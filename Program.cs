// SPDX-License-Identifier: Apache-2.0
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace YtPip;

static class Program
{
    // YTPIP_DATA: another folder (profile, window.txt, log) for a test copy, so it runs beside the one in use.
    public static readonly string DataDir = Environment.GetEnvironmentVariable("YTPIP_DATA") is { Length: > 0 } d ? d
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yt-pip");
    // YTPIP_TEST=1: a test copy that stays out of the way: off every screen, not on top, never activated, no taskbar button.
    internal static readonly bool Test = Environment.GetEnvironmentVariable("YTPIP_TEST") == "1";

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--selftest")) return SelfTest.Run();
        ApplicationConfiguration.Initialize();
        try { File.Delete(Path.Combine(DataDir, "log.txt")); } catch (IOException) { }
        // An error in an event handler: into the log and carry on, instead of a dialog that stops the app.
        Application.ThreadException += (_, e) => Log($"unhandled: {e.Exception}");
        // yt-pip.exe <watch url>: open straight into that video.
        Application.Run(new PipForm(args.FirstOrDefault(IsWatchUrl)));
        return 0;
    }

    // Last run only; enough to see why the PIP did not go fullscreen.
    public static void Log(string line)
    {
        try { File.AppendAllText(Path.Combine(DataDir, "log.txt"), $"{DateTime.Now:HH:mm:ss} {line}\n"); }
        catch (IOException) { }
    }

    internal static bool IsWatchUrl(string? s) => VideoId(s) != null;

    // The v= of a YouTube watch link, else null.
    internal static string? VideoId(string? s) => WatchParam(s, "v");

    // The id of a YouTube Shorts link (/shorts/ID), else null.
    internal static string? ShortsId(string? s) =>
        Uri.TryCreate(s, UriKind.Absolute, out var u) && u.Scheme == "https" && u.Host is "www.youtube.com" or "youtube.com" or "m.youtube.com"
        && u.AbsolutePath.Split('/') is ["", "shorts", var id] && System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Za-z0-9_-]+$") ? id : null;

    // A parameter (v, list, index) of a YouTube watch link, else null.
    internal static string? WatchParam(string? s, string name)
    {
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u) || u.Scheme != "https"
            || u.Host is not ("www.youtube.com" or "youtube.com" or "m.youtube.com") || u.AbsolutePath != "/watch")
            return null;
        foreach (var part in u.Query.TrimStart('?').Split('&'))
            if (part.StartsWith(name + "=") && part.Length > name.Length + 1) return part[(name.Length + 1)..];
        return null;
    }
}

// One window. Browse mode: normal YouTube. PIP mode: YouTube's own fullscreen, which in WebView2
// fills the window rather than the monitor, in a small window of the video's shape. While the video plays in PIP the
// title bar is cut off (WM_NCCALCSIZE); pausing brings it back so the window can be moved.
sealed class PipForm : Form
{
    const int IdOnTop = 0x100, IdMonitorFull = 0x110, IdMusic = 0x120, IdList = 0x130;  // system menu ids: below 0xF000, low 4 bits clear

    enum Mode { Browse, Pip, Monitor, Music }

    // Tells the host when the main player plays or stops (media events do not bubble; capture does).
    const string PlayStateJs = """
        // Not while the time bar is held: YouTube pauses then, and the title bar coming back would change the window
        // under the held button (the release then went missing, 2026-10-05). Told on letting go if it stays paused.
        let onBar = false;
        addEventListener('pointerdown', e => onBar = !!e.target.closest?.('#movie_player .ytp-progress-bar-container'), true);
        addEventListener('pointerup', () => {
          if (!onBar) return;
          onBar = false;
          setTimeout(() => { if (document.querySelector('#movie_player video')?.paused) chrome.webview.postMessage('paused'); }, 300);
        }, true);
        for (const [ev, msg] of [['playing', 'playing'], ['pause', 'paused'], ['ended', 'paused']])
          document.addEventListener(ev, e => {
            if (e.target.closest && e.target.closest('#movie_player, #shorts-player') && !(onBar && msg === 'paused')) chrome.webview.postMessage(msg);
          }, true);
        // The main video's size, so the PIP takes its shape (Shorts are 9:16). Not an ad's: 'playing' after it sends the video's again.
        let ratio = '';
        for (const ev of ['loadedmetadata', 'resize', 'playing'])
          document.addEventListener(ev, e => {
            const v = e.target;
            if (!v.closest?.('#movie_player') || !v.videoWidth || document.querySelector('#movie_player.ad-showing')) return;
            const r = v.videoWidth + 'x' + v.videoHeight;
            if (r !== ratio) chrome.webview.postMessage('ratio:' + (ratio = r));
          }, true);
        // A Short opened as a watch page (a /watch link to it) loops as on YouTube's own Shorts page, instead of autoplay
        // going on to the next video: upright, at most 3 minutes. Never an ad's: it would play forever.
        document.addEventListener('loadedmetadata', e => {
          const v = e.target;
          if (location.host === 'www.youtube.com' && v.closest?.('#movie_player'))
            v.loop = v.videoHeight > v.videoWidth && v.duration <= 181 && !document.querySelector('#movie_player.ad-showing');
        }, true);
        // A Short's comments beside it close again from its comments button, as they opened (YouTube leaves them open).
        addEventListener('click', e => {
          const b = e.target.closest?.('.ytReelPlayerOverlayViewModelActionsContainer button');
          const close = document.querySelector('ytd-shorts #anchored-panel ytd-engagement-panel-section-list-renderer[target-id=engagement-panel-comments-section][visibility=ENGAGEMENT_PANEL_VISIBILITY_EXPANDED] #visibility-button button');
          if (!b || !close || !document.documentElement.classList.contains('yt-pip-cmts') || !/^(댓글|comment)/i.test(b.getAttribute('aria-label') || '')) return;
          e.preventDefault(); e.stopImmediatePropagation();
          close.click();
        }, true);
        // Where the video is, for the list panel's thumbnail bars: YouTube draws its own once, when the panel loads.
        let progress = '';
        document.addEventListener('timeupdate', e => {
          const v = e.target, id = new URLSearchParams(location.search).get('v');
          if (location.host !== 'www.youtube.com' || !id || !v.closest?.('#movie_player') || !v.duration || document.querySelector('#movie_player.ad-showing')) return;
          const m = 'progress:' + id + ':' + Math.round(v.currentTime / v.duration * 100);
          if (m !== progress) chrome.webview.postMessage(progress = m);
        }, true);
        document.addEventListener('DOMContentLoaded', () => document.head.append(Object.assign(document.createElement('style'), {
          textContent: ''
            // The fullscreen playlist button narrows the player for the playlist but leaves the panel `hidden` (any window
            // width, 2026-10-05; why is inside YouTube's script). ponytail: shown while the page says it is open.
            + 'ytd-watch-flexy[fullscreen][playlist-panel-expanded] #panels-full-bleed-container > ytd-playlist-panel-renderer[hidden] { display: flex !important; }'
            // The Shorts page in the PIP (MenuJs marks it): the video over the whole window as on a phone, no top bar, the
            // comments over it. Without this the page is wider than a small PIP and cuts them (288 wide, 2026-10-09).
            + 'html.yt-pip-shorts #masthead-container, html.yt-pip-shorts #cinematic-shorts-scrim { display: none !important; }'
            // nor YouTube's left nav rail (Home, Shorts, Subscriptions, You): the window widened for the comments is wide
            // enough for it and it lay over their top left (2026-10-10)
            + 'html.yt-pip-shorts :is(ytd-mini-guide-renderer, tp-yt-app-drawer#guide) { display: none !important; }'
            + 'html.yt-pip-shorts ytd-app { --ytd-masthead-height: 0px !important; }'
            + 'html.yt-pip-shorts #page-manager.ytd-app, html.yt-pip-shorts ytd-shorts, html.yt-pip-shorts ytd-shorts .reel-video-in-sequence-new { margin: 0 !important; }'
            + 'html.yt-pip-shorts ytd-shorts, html.yt-pip-shorts ytd-shorts * { --ytd-shorts-player-height: 100vh !important; --ytd-shorts-player-width: 100vw !important;'
            + ' --ytd-shorts-masthead-height: 0px !important; }'
            // not on every element: the buttons' icon boxes collapse and the icons slip off their circles
            + 'html.yt-pip-shorts ytd-shorts :is(.reel-video-in-sequence-new, ytd-reel-video-renderer, ytd-player, #sticker-layer, #experiment-overlay, #experiment-overlay > *) { min-width: 0 !important; min-height: 0 !important; }'
            + 'html.yt-pip-shorts ytd-shorts #anchored-panel { left: 0 !important; right: 0 !important; width: auto !important; }'
            + 'html.yt-pip-shorts ytd-shorts #anchored-panel > * { width: 100% !important; }'
            // Comments (any panel) beside the Short: the host widens the window by 360 on the side it says; the video keeps
            // its width (--yt-pip-vw, MenuJs) and the panel shows only once the window has room for it (-ready)
            + 'html.yt-pip-cmts ytd-shorts, html.yt-pip-cmts ytd-shorts * { --ytd-shorts-player-width: var(--yt-pip-vw) !important; }'
            + 'html.yt-pip-cmts ytd-shorts #shorts-container { width: var(--yt-pip-vw) !important; }'
            + 'html.yt-pip-cmts-left ytd-shorts #shorts-container { margin-left: calc(100vw - var(--yt-pip-vw)) !important; }'
            + 'html.yt-pip-shorts:not(.yt-pip-cmts-ready) ytd-shorts #anchored-panel { visibility: hidden !important; }'
            + 'html.yt-pip-cmts ytd-shorts #anchored-panel { position: fixed !important; top: 0 !important; height: 100vh !important; width: 360px !important; left: auto !important; right: 0 !important; }'
            + 'html.yt-pip-cmts-left ytd-shorts #anchored-panel { left: 0 !important; right: auto !important; }'
            + 'html.yt-pip-cmts ytd-shorts #anchored-panel > * { height: 100% !important; max-height: none !important; }'
            // Glass, as YouTube's buttons over the video: the panel see-through over MenuJs's canvas, the video's frame stretched
            // and blurred behind it. Positioned and first in the body, so the panel (positioned, later) paints over it.
            + 'html.yt-pip-cmts-ready ytd-shorts :is(#anchored-panel, #anchored-panel #content, #anchored-panel ytd-item-section-renderer #header) { background: transparent !important; }'
            + '.yt-pip-glass { display: none; }'
            + 'html.yt-pip-cmts-ready .yt-pip-glass { display: block; position: fixed; top: 0; right: 0; width: 360px; height: 100vh;'
            + ' transform: scale(1.2); filter: blur(24px) brightness(0.45) saturate(1.4); }'  // scaled: the blur fades its edges
            + 'html.yt-pip-cmts-left .yt-pip-glass { left: 0; right: auto; }'
            // no dimming over the video, which also took the wheel from it (the next Short)
            + 'html.yt-pip-shorts ytd-shorts #anchored-panel-scrim { display: none !important; }'
            // MenuJs's way back to the big window (the Shorts page has no fullscreen to leave): left of YouTube's ⋮, as it looks
            // (MenuJs moves it beside whatever buttons YouTube has there)
            + '.yt-pip-big { display: none; }'
            + 'html.yt-pip-shorts .yt-pip-big { display: flex; position: fixed; top: 16px; right: 64px; z-index: 2100; width: 48px; height: 48px; padding: 0; border: 0;'
            + ' border-radius: 50%; align-items: center; justify-content: center; background: rgba(0, 0, 0, 0.3); cursor: pointer; }'
            + 'html.yt-pip-shorts .yt-pip-big:hover { background: rgba(0, 0, 0, 0.5); }'
            + 'html.yt-pip-shorts:not(:hover) .yt-pip-big { opacity: 0; transition: opacity 0.2s; }'  // with YouTube's top controls
            + 'html.yt-pip-cmts:not(.yt-pip-cmts-left) .yt-pip-big:not([style*=right]) { right: calc(100vw - var(--yt-pip-vw) + 64px); }'
            // the wider page gets YouTube's desktop layout, buttons right of the video (under the comments): back over its edge
            + 'html.yt-pip-cmts ytd-shorts .ytReelPlayerOverlayViewModelActionsContainer { position: absolute !important; left: calc(var(--yt-pip-vw) - 72px) !important; top: 0 !important; bottom: 0 !important; }'
            + 'html.yt-pip-cmts ytd-shorts .ytReelPlayerOverlayViewModelMetadataContainer { width: calc(var(--yt-pip-vw) - 72px) !important; }' })));
        // Captions start off on every video, whatever YouTube remembers (its own toggle sticks to the next videos).
        // ponytail: decided in the first 3 s the video plays (ads not counted), so C pressed in those seconds is undone.
        if (location.host === 'www.youtube.com') {
          let done = null, cur = null, since = 0;
          setInterval(() => {
            const v = document.querySelector('#movie_player video'), id = new URLSearchParams(location.search).get('v');
            if (!v || !id || id === done || v.paused || document.querySelector('#movie_player.ad-showing')) return;
            if (id !== cur) { cur = id; since = Date.now(); }
            const b = document.querySelector('#movie_player .ytp-subtitles-button');
            if (b?.getAttribute('aria-pressed') === 'true') { b.click(); done = id; }
            else if (Date.now() - since > 3000) done = id;
          }, 300);
        }
        """;

    // In PIP, dragging the video (not the controls) asks the host to move the window; the click
    // that ends the drag is swallowed so it does not pause the video.
    const string DragJs = """
        (() => {
          let start = null, dragging = false, pressed = null;
          addEventListener('mousedown', e => {
            start = null; dragging = false;  // a drag that ends off the video sends no click to swallow
            // on the Shorts page in the PIP the video itself only: its buttons, title and time bar lie over it
            const shorts = document.documentElement.classList.contains('yt-pip-shorts');
            if (e.button !== 0 || !(shorts ? e.target.matches('#shorts-player video') : document.fullscreenElement)) return;
            if (e.target.closest('.ytp-chrome-bottom,.ytp-chrome-top,.ytp-popup,.ytp-settings-menu,button,a,input,#yt-pip-menu')) return;
            start = { x: e.screenX, y: e.screenY };
            pressed = e.target;
          }, true);
          addEventListener('mousemove', e => {
            if (!start || dragging) return;
            if (!(e.buttons & 1)) { start = null; return; }
            if (Math.abs(e.screenX - start.x) + Math.abs(e.screenY - start.y) < 6) return;
            dragging = true;
            // The button stays down while the window follows, which YouTube takes for press-and-hold (2x after ~0.5 s).
            // A mouseup ends its hold, before or after 2x (pointercancel, mouseleave, blur do not; measured 2026-10-05).
            pressed.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, button: 0, clientX: e.clientX, clientY: e.clientY }));
            chrome.webview.postMessage('drag');
          }, true);
          addEventListener('click', e => {
            if (!dragging) return;
            dragging = false; start = null;
            e.stopImmediatePropagation(); e.preventDefault();
          }, true);
          // Pulling up while dragging the time bar opens YouTube's fine scrubbing, which stays after the button is
          // let go (the seek follows the pointer) until Esc or another click. The PIP is small enough that the pull
          // happens by accident, so letting go ends it here: Esc seeks to where it was let go (measured 2026-10-05).
          addEventListener('mouseup', e => {
            if (e.button !== 0 || !e.isTrusted || !document.fullscreenElement) return;
            setTimeout(() => {
              if (document.querySelector('#movie_player.ytp-fine-scrubbing-enable'))
                document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', code: 'Escape', keyCode: 27, bubbles: true }));
            });
          }, true);
        })();
        """;

    // Menu button at the left edge while paused in PIP; in YouTube Music a "back to YouTube" button in its top bar. The host posts
    // {mode: 'pip'|'music'|'none', list, onTop} on every change; picks go back as 'menu:music' / 'menu:list' / 'menu:ontop'.
    const string MenuJs = """
        (() => {
          const isMusic = location.host === 'music.youtube.com';
          let state = { mode: 'none', list: false, onTop: false, comments: null }, host, menu, musicItem, listItem, ctls, layout = '', panel = false, vw = 0, side = null, shown = false, asked, big, glass, paint = 0, lastShort = '', fresh = 0, fsPanel = '';
          // The host's picks in the player's own controls, before autoplay/subtitles/settings (the left-edge menu shows only
          // while paused): 볼 목록 (PIP only), YouTube Music, 항상 위. Icons drawn here, each a set of path outlines.
          const ctlButton = (cmd, ...ds) => {
            const b = Object.assign(document.createElement('button'), { className: 'ytp-button',
              onclick: e => { e.stopPropagation(); chrome.webview.postMessage('menu:' + cmd); } });
            b.style.cssText = 'align-items:center;justify-content:center';
            const ns = 'http://www.w3.org/2000/svg', svg = document.createElementNS(ns, 'svg');
            for (const [k, v] of [['width', 24], ['height', 24], ['viewBox', '0 0 24 24'], ['fill', 'none'], ['stroke', '#fff'], ['stroke-width', 2], ['stroke-linejoin', 'round']]) svg.setAttribute(k, v);
            for (const d of ds) { const p = document.createElementNS(ns, 'path'); p.setAttribute('d', d); svg.append(p); }
            b.append(svg);
            return b;
          };
          const update = () => {
            // the Shorts page in the PIP gets its own layout (PlayStateJs's style); YouTube sizes its player on resize.
            // A panel (comments, description) goes beside it: it opens in the narrow window, the host picks a side, the page
            // lays out for it and asks for the room (placed), the window widens. Through that, and back when it closes,
            // the video keeps its width (vw, CSS px) and the panel shows once it has its room: nothing overlaps or jumps.
            const shorts = !isMusic && state.mode === 'pip' && location.pathname.startsWith('/shorts/'), root = document.documentElement;
            // A Short comes in with its panel closed: YouTube opens again the comments last left open, also after other
            // videos (2026-10-10). Not the next Short by the wheel, where YouTube keeps them; nor once the user clicks.
            if (shorts && !root.classList.contains('yt-pip-shorts') && location.pathname !== lastShort) fresh = Date.now();
            if (shorts) lastShort = location.pathname;
            const expanded = 'ytd-shorts #anchored-panel ytd-engagement-panel-section-list-renderer[visibility=ENGAGEMENT_PANEL_VISIBILITY_EXPANDED]';
            const closing = shorts && Date.now() - fresh < 3000 && document.querySelector(expanded + ' #visibility-button button');
            if (closing) closing.click();
            const open = shorts && !closing && !!document.querySelector(expanded);
            if (!shorts) vw = 0;
            else if (open && !vw) vw = innerWidth;
            if (open && state.comments) side = state.comments;
            // the room is waited for once, as it opens: after that the user's narrowing hid the panel (2026-10-10)
            const ready = shown = !!(open && state.comments && (shown || innerWidth >= vw + 359));
            if (ready) vw = innerWidth - 360;                    // follows the user's resizing
            else if (!open && innerWidth < vw + 300) vw = 0;     // closed and narrowed back
            const key = [shorts, vw, ready, side].join();
            if (layout !== key || root.classList.contains('yt-pip-shorts') !== shorts) {
              layout = key;
              root.classList.toggle('yt-pip-shorts', shorts);
              root.classList.toggle('yt-pip-cmts', !!vw);
              root.classList.toggle('yt-pip-cmts-ready', ready);
              root.classList.toggle('yt-pip-cmts-left', !!vw && side === 'left');
              root.style.setProperty('--yt-pip-vw', vw + 'px');
              dispatchEvent(new Event('resize'));
            }
            if (open && state.comments && !ready && asked !== state) chrome.webview.postMessage((asked = state, 'panel:placed'));
            // opened or closed: the host picks a side or narrows the window back
            if (open !== panel) chrome.webview.postMessage((panel = open) ? 'panel:open' : 'panel:closed');
            // the panel's glass (PlayStateJs's style): a few pixels of the video, 4 times a second while the panel shows
            if (ready && !glass && document.body) document.body.prepend(glass = Object.assign(document.createElement('canvas'), { className: 'yt-pip-glass', width: 16, height: 28 }));
            if (ready !== !!paint) {
              clearInterval(paint);
              paint = ready ? setInterval(() => { const v = document.querySelector('#shorts-player video'); if (v?.videoWidth) glass.getContext('2d').drawImage(v, 0, 0, 16, 28); }, 250) : 0;
            }
            if (shorts && !big && document.body) {
              big = Object.assign(ctlButton('browse', 'M19 12H5M11 6l-6 6 6 6'), { className: 'yt-pip-big', title: '돌아가기' });  // an arrow back
              big.setAttribute('aria-label', '돌아가기');
              document.body.append(big);
            }
            // left of YouTube's buttons at the top right, which vary (⋮ alone; CC and expand signed in, which it covered,
            // 2026-10-10): beside them wherever they are, the CSS's place when there are none
            const rc = shorts && big && [...document.querySelectorAll('ytd-shorts #right-controls')]
              .map(e => e.getBoundingClientRect()).find(r => r.width && r.top >= 0 && r.top < innerHeight / 2);
            if (big) { big.style.right = rc ? innerWidth - rc.left + 8 + 'px' : ''; big.style.top = rc ? rc.top + (rc.height - 48) / 2 + 'px' : ''; }
            const group = !isMusic && document.querySelector('#movie_player .ytp-right-controls-left');
            if (group && !ctls) ctls = [
              ctlButton('list', 'M4 5h16v14H4zM14 5v14'),                                               // a window with a panel on its right
              ctlButton('music', 'M10 17V5l9-2v12', 'M10 17a2.5 2.5 0 1 1-5 0a2.5 2.5 0 1 1 5 0', 'M19 15a2.5 2.5 0 1 1-5 0a2.5 2.5 0 1 1 5 0'),  // a note pair
              ctlButton('ontop', 'M4 14V4h11v4', 'M9 9h11v11H9z')];                                    // a window over another
            if (group && ctls[0].parentNode !== group) group.prepend(...ctls);
            if (ctls) {
              const [list, music, onTop] = ctls;
              list.style.display = state.mode === 'pip' ? 'inline-flex' : 'none';
              list.title = state.list ? '볼 목록 닫기' : '볼 목록 열기';
              music.title = 'YouTube Music으로 전환';
              onTop.title = state.onTop ? '항상 위에 표시 끄기' : '항상 위에 표시';
              onTop.querySelectorAll('path')[1].setAttribute('fill', state.onTop ? 'rgba(255,255,255,.45)' : 'none');  // filled while on
              for (const b of ctls) b.setAttribute('aria-label', b.title);
            }
            // For the log: what a panel over the fullscreen video is made of, 2 s after it opens (the title opened an empty
            // one for the user, signed in; signed out it shows the description, 2026-10-10)
            const flexy = !shorts && document.querySelector('ytd-watch-flexy[fullscreen]');
            const fsKey = flexy ? [...flexy.attributes].map(a => a.name).filter(n => n.includes('panel')).join(' ') : '';
            if (fsKey !== fsPanel && (fsPanel = fsKey)) setTimeout(() => {
              const desc = e => `${e.tagName.toLowerCase()}${e.getAttribute('target-id') ? '#' + e.getAttribute('target-id') : ''} ${e.hidden ? 'hidden ' : ''}${getComputedStyle(e).display} ${Math.round(e.getBoundingClientRect().width)}x${Math.round(e.getBoundingClientRect().height)} text ${e.innerText.trim().length}`;
              const kids = [...document.querySelectorAll('#panels-full-bleed-container > *, ytd-watch-flexy[fullscreen] ytd-engagement-panel-section-list-renderer[visibility=ENGAGEMENT_PANEL_VISIBILITY_EXPANDED]')];
              chrome.webview.postMessage(`log:fullscreen panel [${fsKey}] ` + kids.filter(e => e.getBoundingClientRect().width).map(desc).join(' | '));
            }, 2000);
            if (!host) return;
            const v = document.querySelector(shorts ? '#shorts-player video' : '#movie_player video') || document.querySelector('video');
            // in Music always: there it is the only way back besides the title bar menu
            const show = isMusic ? state.mode === 'music' : state.mode === 'pip' && v && v.paused && (document.fullscreenElement || shorts);
            // in Music it sits in the top bar before the cast/settings icons, so the bar lays it out (put back if re-rendered)
            // (the host keeps the Music window wide enough that its mobile layout never hides the bar)
            const bar = isMusic && document.querySelector('ytmusic-nav-bar #right-content');
            if (bar && host.parentNode !== bar) bar.prepend(host);
            host.style.display = show ? (isMusic ? 'flex' : 'block') : 'none';
            if (!show) menu.classList.remove('open');
            musicItem.textContent = isMusic ? 'YouTube로 전환' : 'YouTube Music으로 전환';
            listItem.textContent = state.list ? '볼 목록 닫기' : '볼 목록 열기';
            listItem.style.display = isMusic ? 'none' : 'block';
          };
          const build = () => {
            host = document.createElement('div');
            host.id = 'yt-pip-menu';
            // left middle over the video; in Music a plain item of its top bar (see update)
            host.style.cssText = isMusic ? 'align-items:center;margin:0 8px;display:none'
              : 'left:12px;top:50%;transform:translateY(-50%);position:fixed;z-index:2147483647;display:none';
            const root = host.attachShadow({ mode: 'open' });
            // YouTube enforces Trusted Types: no innerHTML strings, build the nodes.
            const el = (tag, props, ...kids) => { const n = Object.assign(document.createElement(tag), props); n.append(...kids); return n; };
            const style = el('style', { textContent: `
                .b { width: 40px; height: 40px; border-radius: 50%; border: 0; background: rgba(0,0,0,.6); cursor: pointer; display: grid; place-items: center; }
                .b:hover { background: rgba(0,0,0,.85); }
                .m { position: absolute; left: 48px; top: 50%; transform: translateY(-50%); display: none; padding: 6px; border-radius: 10px;
                     background: rgba(28,28,28,.96); box-shadow: 0 4px 16px rgba(0,0,0,.5); }
                .m.open { display: block; }
                .pill { width: auto; height: 36px; padding: 0 16px; border-radius: 18px; background: rgba(255,255,255,.1);
                        color: #fff; font: 500 14px "Segoe UI", sans-serif; white-space: nowrap; }
                .pill:hover { background: rgba(255,255,255,.2); }                .m button { display: block; width: 100%; padding: 9px 14px; border: 0; border-radius: 6px; background: none; color: #fff;
                            font: 14px "Segoe UI", sans-serif; text-align: left; white-space: nowrap; cursor: pointer; }
                .m button:hover { background: rgba(255,255,255,.12); }` });
            const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            for (const [k, v] of [['width', 18], ['height', 18], ['viewBox', '0 0 18 18'], ['fill', '#fff']]) svg.setAttribute(k, v);
            for (const y of [3, 8, 13]) {
              const r = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
              for (const [k, v] of [['y', y], ['width', 18], ['height', 2], ['rx', 1]]) r.setAttribute(k, v);
              svg.append(r);
            }
            // Music has a single pick, so the button is that pick
            const button = isMusic ? el('button', { className: 'b pill', textContent: 'YouTube로 전환', onclick: () => chrome.webview.postMessage('menu:music') })
              : el('button', { className: 'b', title: '메뉴', onclick: () => menu.classList.toggle('open') }, svg);
            musicItem = el('button'); musicItem.dataset.cmd = 'music';
            listItem = el('button'); listItem.dataset.cmd = 'list';
            menu = el('div', { className: 'm' }, musicItem, listItem);
            root.append(style, button, menu);
            menu.onclick = e => {
              const cmd = e.target.dataset && e.target.dataset.cmd;
              if (!cmd) return;
              menu.classList.remove('open');
              chrome.webview.postMessage('menu:' + cmd);
            };
            // keep the page (YouTube's player, its keyboard shortcuts) from reacting to the menu
            for (const t of ['mousedown', 'mouseup', 'click', 'dblclick']) host.addEventListener(t, e => e.stopPropagation());
            document.documentElement.appendChild(host);
            update();
          };
          chrome.webview.addEventListener('message', e => { state = e.data; update(); });
          addEventListener('click', e => { if (e.isTrusted) fresh = 0; }, true);
          for (const t of ['play', 'playing', 'pause', 'ended']) document.addEventListener(t, update, true);
          document.addEventListener('fullscreenchange', update);
          addEventListener('resize', update);
          new MutationObserver(update).observe(document, { subtree: true, attributes: true, attributeFilter: ['visibility'] });  // panels, at once
          setInterval(update, 500);
          if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', build); else build();
        })();
        """;

    // In the list panel: a picked video plays in the PIP instead of opening in the panel. One picked in a
    // playlist plays on through it there, and the panel shows that playlist.
    const string ListJs = """
        addEventListener('click', e => {
          const a = e.target.closest && e.target.closest('a[href*="/watch?v="], a[href*="/shorts/"]');
          if (!a) return;
          e.preventDefault(); e.stopImmediatePropagation();
          const u = new URL(a.href);
          // a Short plays on YouTube's Shorts page in the PIP, as one picked in the big window does
          if (u.pathname.startsWith('/shorts/')) return chrome.webview.postMessage('https://www.youtube.com/shorts/' + u.pathname.split('/')[2]);
          const id = u.searchParams.get('v');
          if (!id) return;
          const list = u.searchParams.get('list'), index = u.searchParams.get('index');
          // A playlist's own link (its title, thumbnail: the entry's first link; in search results /watch?v=<first>&list=,
          // 2026-10-10) opens the playlist: the host plays its first video only if the PIP is not in that playlist already.
          // By the href, not the entry the link is in: a click after hovering lands on YouTube's inline preview, a link
          // to the same place outside the entry (2026-10-10: the first video played every time).
          const own = list && !index && [...document.querySelectorAll('yt-lockup-view-model, ytd-playlist-renderer, ytd-radio-renderer')]
            .some(b => b.querySelector('a[href*="list="]')?.href === a.href);
          if (own) chrome.webview.postMessage('playlist:' + list + ':' + id);
          else chrome.webview.postMessage('https://www.youtube.com/watch?v=' + id + (list ? '&list=' + list + (index ? '&index=' + index : '') : ''));
          // not a Mix (RD...): YouTube has no page for those ("볼 수 없는 재생목록 유형", 2026-10-05)
          if (list && !list.startsWith('RD') && new URLSearchParams(location.search).get('list') !== list) location.assign('/playlist?list=' + list);
        }, true);
        // The PIP's position on the thumbnails of the videos it played, over YouTube's bar from the page load, and the
        // one it plays now highlighted (kept through the panel's own navigation: drawn again every second).
        const watched = new Map();
        let now = null;
        chrome.webview.addEventListener('message', e => {
          const m = /^progress:([\w-]{11}):(\d{1,3})$/.exec(e.data);
          if (m) { watched.set(now = m[1], Math.min(100, +m[2])); drawWatched(); }
        });
        const item = 'yt-lockup-view-model, ytd-rich-item-renderer, ytd-playlist-video-renderer, ytd-video-renderer, ytd-compact-video-renderer';
        const drawWatched = () => {
          for (const e of document.querySelectorAll('.yt-pip-now'))
            if (!e.querySelector(`a[href*="v=${now}"]`)) e.classList.remove('yt-pip-now');
          if (now) for (const a of document.querySelectorAll(`a[href*="v=${now}"]`)) a.closest(item)?.classList.add('yt-pip-now');
          for (const [id, p] of watched)
            for (const a of document.querySelectorAll(`a[href*="v=${id}"]`)) {
              const host = a.querySelector('yt-thumbnail-view-model') ?? a.closest('ytd-thumbnail');
              if (!host) continue;
              let bar = host.querySelector(':scope > .yt-pip-watched');
              if (!bar) { bar = Object.assign(document.createElement('div'), { className: 'yt-pip-watched' }); bar.append(document.createElement('div')); host.append(bar); }
              bar.firstChild.style.width = p + '%';
            }
        };
        setInterval(drawWatched, 1000);
        // At this width the search box folds behind an arrow labelled "뒤로" that only closes the box, leaving the
        // results up; on the results page it goes back too (YouTube's own handler still closes the box).
        addEventListener('click', e => {
          if (e.target.closest?.('ytd-masthead #back-button') && location.pathname === '/results' && navigation.canGoBack) history.back();
        }, true);
        // YouTube's "continue watching" miniplayer would cover the list; at this width the logo runs into
        // the search button, and "만들기" only crowds the bar.
        document.addEventListener('DOMContentLoaded', () => document.head.append(Object.assign(document.createElement('style'), {
          textContent: 'ytd-miniplayer, ytd-masthead #logo, ytd-masthead #buttons > ytd-button-renderer { display: none !important; }'
            // the bar YouTube draws for a watched video, a red strip along the thumbnail's bottom edge, covered whole
            + '.yt-pip-watched { position: absolute; left: 0; right: 0; bottom: 0; height: 4px; z-index: 10; background: #717171; pointer-events: none; }'
            + '.yt-pip-watched > div { height: 100%; background: #f03; }'
            + '.yt-pip-now { background: rgba(255, 255, 255, .12); box-shadow: 0 0 0 6px rgba(255, 255, 255, .12); border-radius: 12px; outline: 2px solid #f03; outline-offset: 6px; }' })));
        // At least two columns however narrow the panel; YouTube itself drops to one below ~550px.
        const columns = () => {
          const g = document.querySelector('ytd-rich-grid-renderer');
          if (g) g.style.setProperty('--ytd-rich-grid-items-per-row', Math.max(2, Math.floor(innerWidth / 300)), 'important');
        };
        // Close and back buttons before the masthead's menu button (put back if YouTube re-renders the bar); back only
        // when there is somewhere to go back to (a search, a playlist). DOM nodes, not markup: YouTube enforces Trusted Types.
        const button = (title, d, onclick) => {
          const b = Object.assign(document.createElement('button'), { title, onclick });
          b.style.cssText = 'width:40px;height:40px;margin-right:4px;border:0;border-radius:50%;background:none;cursor:pointer;display:grid;place-items:center';
          b.onmouseenter = () => b.style.background = 'rgba(255,255,255,.1)';
          b.onmouseleave = () => b.style.background = 'none';
          const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
          for (const [k, v] of [['width', 20], ['height', 20], ['viewBox', '0 0 20 20'], ['fill', 'none'], ['stroke', '#fff'], ['stroke-width', 2], ['stroke-linecap', 'round'], ['stroke-linejoin', 'round']]) svg.setAttribute(k, v);
          const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
          path.setAttribute('d', d);
          svg.append(path);
          b.append(svg);
          return b;
        };
        const close = button('볼 목록 닫기', 'M4 4l12 12M16 4L4 16', () => chrome.webview.postMessage('close'));
        const back = button('뒤로', 'M16 10H4M9 5l-5 5 5 5', () => history.back());
        const bar = () => {
          const start = document.querySelector('ytd-masthead #start');
          if (!start) return;
          if (close.parentNode !== start) { start.prepend(close); close.after(back); }
          back.style.display = navigation.canGoBack ? 'grid' : 'none';
        };
        addEventListener('resize', columns);
        setInterval(() => { columns(); bar(); }, 1000);
        addEventListener('yt-navigate-finish', bar);
        addEventListener('pageshow', bar);
        """;

    // Switching between YouTube and YouTube Music (separate sites, separate saved volume): once the player has the video
    // ($ID, null: any), to the position ($T, -1: as it is) and volume ($V, -1: as it is), playing; resolves once it plays,
    // so the other side's sound stops only then. Player API, since video.volume is YouTube's loudness-normalised value.
    const string ResumeJs = """
        new Promise(done => {
          const id = $ID, t = $T, t0 = Date.now();
          let seen = 0, was = '';
          const i = setInterval(() => {
            if (Date.now() - t0 > 15000) { clearInterval(i); return done('timeout' + (seen ? ', player at ' + (seen - t0) + 'ms' : '')); }
            const p = document.querySelector(location.pathname.startsWith('/shorts/') ? '#shorts-player' : '#movie_player'), v = p?.querySelector('video') ?? document.querySelector('video');
            if (!v || v.readyState < 1 || !p?.setVolume || id && p.getVideoData?.()?.video_id !== id) return;
            if (!seen) {
              seen = Date.now();
              was = (v.paused ? 'paused' : 'playing') + ' at ' + v.currentTime.toFixed(1);
              if (t >= 0 && Math.abs(v.currentTime - t) > 1.5) v.currentTime = t;
              if ($V >= 0) { p.setVolume($V); $M ? p.mute() : p.unMute(); }
              p.playVideo?.();
            }
            if (v.paused || v.readyState < 3) return;
            clearInterval(i);
            done('playing at ' + (Date.now() - t0) + 'ms, player at ' + (seen - t0) + 'ms, page ' + Math.round(performance.now()) + 'ms, was ' + was);
          }, 30);
        })
        """;

    // The side switched away from stops (its own pause, as its button does). Shorts too.
    const string PauseJs = "for (const p of document.querySelectorAll('#movie_player, #shorts-player')) p.pauseVideo?.()";

    // Waits until the video plays, then true; the host presses F, YouTube's own fullscreen key, with real key
    // input. Fullscreening the player directly makes YouTube drop it again, a script b.click() fullscreens <html>
    // without switching the page to its fullscreen layout, and in a window the PIP's size the player hides its
    // fullscreen button (0x0, 2026-10-05).
    const string FullscreenJs = """
        new Promise(done => {
          const t0 = Date.now();
          const t = setInterval(() => {
            const v = document.querySelector('#movie_player video');
            if (!v || v.paused || v.currentTime < 0.2) return;
            if (document.fullscreenElement) { clearInterval(t); return done(null); }
            // On a fresh load a press before the watch page has taken the video (its video-id) gets the broken layout
            // (2026-10-05, 6/6 before). After 5 s press anyway.
            // A watch page that had a video before (back from a Short) takes the new one ~1 s after it plays, and a press
            // then is fine (2026-10-10): only a page's first video waits.
            const had = document.querySelector('ytd-watch-flexy')?.getAttribute('video-id');
            const ready = had === new URLSearchParams(location.search).get('v') || !!had;
            if (!ready && Date.now() - t0 < 5000) return;
            clearInterval(t);
            // F typed into a focused box (search, comment) would be text, not the key
            if (document.activeElement?.matches('input, textarea, [contenteditable]:not([contenteditable=false])')) document.activeElement.blur();
            done(true);
          }, 100);
          setTimeout(() => { clearInterval(t); done(null); }, 15000);
        })
        """;

    // While a new video gets to the PIP (~1.4 s, 0.5 s of it playing already, 2026-10-10): over the page its thumbnail,
    // then the video itself drawn on a canvas once it plays, so it shows at once instead of the window's colour. It lets
    // the mouse through (and elementFromPoint looks past it), so the fullscreen button is still found and pressed.
    // Only the new video is drawn (the home page's hover preview is a #movie_player too).
    // Its own view-transition-name: YouTube cross-fades the watch page in a view transition once the page under it has
    // loaded, drawn in the top layer, and the cover in the root snapshot showed under the player and bar for ~200 ms
    // (2026-10-10); as its own group it is drawn last, over the others.
    const string CoverJs = """
        (() => {
          if (document.getElementById('yt-pip-cover')) return;
          const id = new URLSearchParams(location.search).get('v');
          const c = Object.assign(document.createElement('canvas'), { id: 'yt-pip-cover' });
          c.style.cssText = 'position:fixed;inset:0;width:100%;height:100%;z-index:2147483647;pointer-events:none;view-transition-name:yt-pip-cover;background:#181818 center/contain no-repeat'
            + (/^[\w-]+$/.test(id) ? ` url("https://i.ytimg.com/vi/${id}/mqdefault.jpg")` : '');
          document.documentElement.append(c);
          // the page's scrollbar beside it otherwise, until fullscreen (2026-10-10)
          if (!document.getElementById('yt-pip-cover-css')) document.documentElement.append(Object.assign(document.createElement('style'),
            { id: 'yt-pip-cover-css', textContent: 'html:has(> #yt-pip-cover) { scrollbar-width: none }' }));
          const g = c.getContext('2d');
          const draw = () => {
            if (!c.isConnected) return;
            const p = document.querySelector('#movie_player'), v = p?.querySelector('video');
            if (v && !v.paused && v.readyState >= 2 && v.videoWidth && p.getVideoData?.()?.video_id === id) {
              const w = Math.round(innerWidth * devicePixelRatio), h = Math.round(innerHeight * devicePixelRatio);
              if (c.width !== w || c.height !== h) { c.width = w; c.height = h; }
              const s = Math.min(w / v.videoWidth, h / v.videoHeight), vw = v.videoWidth * s, vh = v.videoHeight * s;
              g.fillStyle = '#000'; g.fillRect(0, 0, w, h);
              g.drawImage(v, (w - vw) / 2, (h - vh) / 2, vw, vh);
            }
            requestAnimationFrame(draw);
          };
          draw();
        })()
        """;
    // In fullscreen the watch page's bar is still what shows at the top for 30+ ms after its fullscreen layout is on
    // (2026-10-10, 30+ ms): the cover stays until the bar is no longer what shows there (1 s cap). Not "until the player
    // shows there": with the list open YouTube's side panel box is there instead (it waited the full second, 2026-10-10).
    // setTimeout, not requestAnimationFrame: a hidden window would never finish.
    const string UncoverJs = """
        new Promise(done => {
          const t0 = performance.now();
          const go = () => {
            const c = document.getElementById('yt-pip-cover');
            if (c && document.fullscreenElement && document.elementFromPoint(innerWidth / 2, 8)?.closest('ytd-masthead, #masthead-container')
              && performance.now() - t0 < 1000) return setTimeout(go, 16);
            c?.remove(); done(null);
          };
          go();
        })
        """;

    // After the button press: "ok", "broken" (fullscreen without YouTube's fullscreen layout) or "none".
    const string LayoutJs = """
        new Promise(done => {
          const t0 = performance.now();
          let t1 = null;
          const t = setInterval(() => {
            // the page switches to its fullscreen layout shortly after; 800 ms without it is the broken case
            if (document.fullscreenElement) {
              t1 ??= performance.now();
              if (document.querySelector('ytd-watch-flexy[fullscreen]')) { clearInterval(t); done('ok'); }
              else if (performance.now() - t1 > 800) { clearInterval(t); done('broken'); }
            } else if (performance.now() - t0 > 3000) { clearInterval(t); done('none'); }
          }, 100);
        })
        """;

    readonly WebView2 web = new() { Dock = DockStyle.Fill };
    // YouTube Music in its own view, so neither side reloads on a switch (a reload was 1.4-1.7 s of no sound into Music and
    // 1.8-5.2 s to the video back, 2026-10-10). Made on the first switch, kept after.
    // ponytail: its renderer stays (some 300 MB, not measured); drop it after a while in YouTube if memory matters.
    readonly WebView2 music = new() { Dock = DockStyle.Fill, Visible = false };
    readonly WebView2 list = new() { Dock = DockStyle.Right, Visible = false };  // "볼 목록" beside the PIP (side set in ShowList)
    CoreWebView2Environment? env;
    string? userCss;
    bool listOpen;  // session only, like monitorFull
    Point listShift;  // how far ShowList slid the video to fit the panel; closing slides it back
    (string v, int t, int vol, bool muted)? pendingRestore;  // from Music to another video: applied once the page has it
    Mode backMode;  // the YouTube side's mode as it was left for Music
    int switches;  // counts switches: one waiting for its side to play knows when another came after it
    (string Asked, string Got)? musicTrack;  // the video last taken to Music, and the id Music plays it under
    readonly State state = new(Path.Combine(Program.DataDir, "window.txt"));
    readonly string? startUrl;
    Mode mode;
    bool pip => mode == Mode.Pip;
    bool playing;
    bool monitorFull;  // fullscreen fills the monitor like the web player instead of going PIP; off every launch
    string? lastVideo;  // auto-PIP only when a different video starts, or the same one again after leaving its page
    readonly System.Windows.Forms.Timer dragTimer = new() { Interval = 10 };
    Size dragOffset;
    bool retrying;
    double aspect = 16.0 / 9;  // width / height of the video playing, from the page

    bool wasShorts;  // the last address was a Shorts page: moving on to the next Short leaves the window as it is
    bool OnShorts => Program.ShortsId(web.CoreWebView2?.Source) != null;
    double Aspect => OnShorts ? 9.0 / 16 : aspect;  // the Shorts page lays its video out 9:16 whatever the video's shape

    bool TitleHidden => pip && playing;
    protected override bool ShowWithoutActivation => Program.Test;

    public PipForm(string? startUrl)
    {
        this.startUrl = startUrl;
        Text = "YouTube";
        Icon = new Icon(typeof(PipForm).Assembly.GetManifestResourceStream("app.ico")!);
        BackColor = Color.FromArgb(24, 24, 24);
        StartPosition = FormStartPosition.Manual;
        state.Load();
        Bounds = state.BrowseRect;
        TopMost = state.OnTop;
        // Windows makes a window topmost only while its process may take the foreground, and ignores it otherwise
        // (SetWindowPos says so; it still returns success). Started in the background, as replacing the installed
        // copy does, the window came up below others until "항상 위에 표시" was turned off and on (2026-10-10).
        // The first activation (a click on it) gives that right: take topmost then.
        Activated += (_, _) => { if (state.OnTop) SetWindowPos(Handle, -1, 0, 0, 0, 0, 0x13); };  // HWND_TOPMOST; NOSIZE | NOMOVE | NOACTIVATE
        ShowInTaskbar = !Program.Test;
        Controls.Add(web);
        Controls.Add(music);
        Controls.Add(list);  // added after the Fill controls so docking gives it the right edge first
        dragTimer.Tick += (_, _) => DragTick();
        Load += async (_, _) => await InitAsync();
        FormClosing += (_, _) => { RememberRect(); state.Save(); };
    }

    async Task InitAsync()
    {
        // Own profile folder so the YouTube login survives restarts and rebuilds.
        // Autoplay so a picked video starts (and can go PIP) by itself.
        env = await CoreWebView2Environment.CreateAsync(null, Program.DataDir,
            new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));
        await web.EnsureCoreWebView2Async(env);
        var core = web.CoreWebView2;
        // The user's own CSS for the pages (user.css in the data folder), read at start: what else to hide is theirs to
        // choose, not the app's (CLAUDE.md, YouTube 정책).
        try
        {
            userCss = File.ReadAllText(Path.Combine(Program.DataDir, "user.css"));
            Program.Log($"user.css: {userCss.Length} chars");
        }
        catch (IOException) { }  // none: nothing to add
        await SetUpAsync(core, PlayStateJs, DragJs, MenuJs);
        core.WebMessageReceived += async (_, e) =>
        {
            var msg = e.TryGetWebMessageAsString();
            switch (msg)
            {
                case "drag": if (pip) StartDrag(); return;
                case "menu:list": ToggleList(); return;
                case "menu:music": await ToggleMusicAsync(); return;
                case "menu:ontop": ToggleOnTop(); return;
                case "menu:browse": if (pip && OnShorts) SetMode(Mode.Browse); return;  // as leaving fullscreen does on a watch page
                case "panel:open": ShowComments(); return;
                case "panel:closed": HideComments(); return;
                case "panel:placed": PlaceComments(); return;
                case not null when msg.StartsWith("progress:"): list.CoreWebView2?.PostWebMessageAsString(msg); return;
                case not null when msg.StartsWith("log:"): Program.Log(msg[4..Math.Min(msg.Length, 600)].ReplaceLineEndings(" ")); return;
                case not null when msg.StartsWith("ratio:"):
                {
                    var wh = msg[6..].Split('x');
                    if (wh.Length != 2 || !int.TryParse(wh[0], out var vw) || !int.TryParse(wh[1], out var vh) || vw <= 0 || vh <= 0) return;
                    var a = Math.Clamp((double)vw / vh, 0.4, 2.5);  // whatever the page sends, a shape a window can take
                    if (a == aspect) return;
                    aspect = a;
                    Program.Log($"video ratio {vw}x{vh}");
                    if (pip) FitAspect();
                    return;
                }
            }
            playing = msg == "playing";
            if (pip) ApplyFrame();
        };
        core.ContainsFullScreenElementChanged += (_, _) =>
        {
            Program.Log($"fullscreen changed: {core.ContainsFullScreenElement}");
            // The Shorts page's own fullscreen (a hidden button, double-click) is not the Shorts PIP, which is the page as
            // it is: drop it, going PIP from the big window as a watch page does. Left on, '큰 창으로' brought the page's
            // fullscreen into the big window (2026-10-10). Leaving a watch page for a Short drops fullscreen too: no change.
            if (OnShorts)
            {
                if (!core.ContainsFullScreenElement) return;
                _ = EvalAsync("document.exitFullscreen()");
                if (mode == Mode.Browse && !monitorFull) SetMode(Mode.Pip);
                return;
            }
            // YouTube Music has no fullscreen; leaving YouTube for it drops fullscreen, which is not "go back to browsing".
            if (mode == Mode.Music || retrying && !core.ContainsFullScreenElement) return;
            SetMode(!core.ContainsFullScreenElement ? Mode.Browse : monitorFull ? Mode.Monitor : Mode.Pip);
            ShowList();  // back from Music the window was PIP already, so SetMode did nothing
        };
        // A link to YouTube Music (YouTube's own menu has one) opens in Music's view.
        core.NavigationStarting += (_, e) =>
        {
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var to) || to.Host != "music.youtube.com") return;
            e.Cancel = true;
            _ = EnterMusicAsync(e.Uri, null, 0, -1, false);
        };
        // YouTube navigates in-page; a new video picked while browsing goes PIP once it plays
        // (not with monitor fullscreen on: then it behaves like the web player).
        core.SourceChanged += async (_, _) =>
        {
            // YouTube's own Shorts page in the PIP, not fullscreen (it has none): its wheel goes to the next Short, comments
            // open over the video and it loops. A watch page has none of that (2026-10-09). Next Shorts keep the window.
            if (Program.ShortsId(core.Source) != null)
            {
                lastVideo = null;  // a video after it is new
                retrying = false;  // from the list: the PIP was kept while the watch page left fullscreen
                if (!wasShorts && mode == Mode.Browse && !monitorFull) SetMode(Mode.Pip);
                else if (!wasShorts && pip) FitAspect();  // from a video in the PIP (the list): the Short's shape
                wasShorts = true;
                return;
            }
            bool covered = false;
            if (wasShorts)
            {
                wasShorts = false;
                HideComments();
                // Off the Shorts page the big window, unless a video is next (the list, back): fullscreen in the PIP,
                // covered meanwhile as from the big window (else the watch page shows in it for 1.3-2.7 s, 2026-10-10).
                if (pip && Program.VideoId(core.Source) == null) SetMode(Mode.Browse);
                else if (pip) { retrying = true; covered = true; await EvalAsync(CoverJs); FitAspect(); }
            }
            var id = Program.VideoId(core.Source);
            // Off the watch page (back to the home page, say): coming back to the same video, forward, is PIP again.
            // Staying on it out of fullscreen (Esc) changes no address, so that stays as it is.
            if (id == null) { lastVideo = null; return; }
            if (id == lastVideo) return;
            lastVideo = id;
            Program.Log($"new video {id} pip={pip}");
            if (mode == Mode.Browse && !monitorFull)
            {
                // Into the PIP's window at once, covered until the video is fullscreen in it. Else the watch page shows in
                // the big window for the ~2 s the press waits, then the big fullscreen for 0.4 s before the shrink (2026-10-05).
                // Covered first: shrunk first, the small window showed the bare watch page for a frame or more.
                retrying = true;  // keeps the PIP while it is not fullscreen yet
                await EvalAsync(CoverJs);
                SetMode(Mode.Pip);
                await EnterFullscreenAsync();
                if (!core.ContainsFullScreenElement && mode == Mode.Pip) SetMode(Mode.Browse);  // it never got there: the page as it is
                await EvalAsync(UncoverJs);
            }
            // `retrying` here: a video picked from the list or from Music, the window is already PIP. Covered unless its
            // fullscreen stayed (YouTube's in-page navigation keeps it), as from the big window.
            else if (retrying)
            {
                _ = RestoreAsync();
                if (!core.ContainsFullScreenElement)
                {
                    covered = true;
                    await EvalAsync("new Promise(r => { const f = () => document.documentElement ? r(" + CoverJs + ") : setTimeout(f, 16); f(); })");
                }
                await EnterFullscreenAsync();
                if (covered) await EvalAsync(UncoverJs);
            }
            else if (covered) await EvalAsync(UncoverJs);
        };
        core.NavigationCompleted += async (_, _) =>
        {
            SendMenuState();
            await RestoreAsync();
        };
        web.Source = new Uri(startUrl ?? "https://www.youtube.com/");
    }

    // What both views get: the page scripts, the user's CSS, the window title, and dialogs.
    async Task SetUpAsync(CoreWebView2 c, params string[] scripts)
    {
        foreach (var js in scripts) await c.AddScriptToExecuteOnDocumentCreatedAsync(js);
        if (userCss != null)
            await c.AddScriptToExecuteOnDocumentCreatedAsync("document.addEventListener('DOMContentLoaded', () => document.head.append("
                + $"Object.assign(document.createElement('style'), {{ id: 'yt-pip-user-css', textContent: {JsonSerializer.Serialize(userCss)} }})))");
        // YouTube Music asks "leave site?" on every navigation away, which stalls loading another track.
        // Without the default dialogs, alert/confirm become message boxes.
        c.Settings.AreDefaultScriptDialogsEnabled = false;
        c.ScriptDialogOpening += (_, e) =>
        {
            if (e.Kind == CoreWebView2ScriptDialogKind.Beforeunload ||
                MessageBox.Show(this, e.Message, Text, e.Kind == CoreWebView2ScriptDialogKind.Alert ? MessageBoxButtons.OK : MessageBoxButtons.OKCancel) == DialogResult.OK)
                e.Accept();
        };
        c.DocumentTitleChanged += (_, _) => ShowTitle();
    }

    void ShowTitle() => Text = (mode == Mode.Music ? music : web).CoreWebView2?.DocumentTitle is { Length: > 0 } t ? t : "YouTube";

    Task<CoreWebView2> MusicAsync() => musicInit ??= InitMusicAsync();
    Task<CoreWebView2>? musicInit;

    async Task<CoreWebView2> InitMusicAsync()
    {
        await music.EnsureCoreWebView2Async(env);
        var c = music.CoreWebView2;
        await SetUpAsync(c, MenuJs);
        c.WebMessageReceived += async (_, e) =>
        {
            var msg = e.TryGetWebMessageAsString();
            if (msg == "menu:music") await ToggleMusicAsync();
            else if (msg?.StartsWith("log:") == true) Program.Log(msg[4..Math.Min(msg.Length, 600)].ReplaceLineEndings(" "));
        };
        // Back from Music's first page reaches the blank page under it: that is the way back to YouTube. A link to YouTube
        // opens there. Not /musicpremium: where Music sends a browser it does not serve (signed out), its own page.
        c.NavigationStarting += (_, e) =>
        {
            if (e.NavigationKind == CoreWebView2NavigationKind.BackOrForward && e.Uri == "about:blank")
            {
                e.Cancel = true;
                _ = LeaveMusicAsync(null);
            }
            else if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var to) && to.Host is "www.youtube.com" or "youtube.com" && to.AbsolutePath != "/musicpremium")
            {
                e.Cancel = true;
                _ = LeaveMusicAsync(e.Uri);
            }
        };
        c.NavigationCompleted += (_, _) => SendMenuState();
        var blank = new TaskCompletionSource();
        void Blank(object? s, CoreWebView2NavigationCompletedEventArgs e) { c.NavigationCompleted -= Blank; blank.TrySetResult(); }
        c.NavigationCompleted += Blank;
        c.Navigate("about:blank");
        await blank.Task;
        return c;
    }

    // The first fullscreen after a page load often fullscreens <html> while the watch page keeps its
    // normal layout (small video, title and buttons under it). Leaving and pressing again fixes it;
    // `retrying` keeps the window in PIP meanwhile.
    async Task EnterFullscreenAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (await EvalAsync(FullscreenJs) is not { ValueKind: JsonValueKind.True })
            {
                // cut short by a reload (not the script's own 15 s wait, not fullscreen already): wait on the new page
                if (sw.ElapsedMilliseconds < 15000 && !web.CoreWebView2.ContainsFullScreenElement) { await Task.Delay(500); continue; }
                break;
            }
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",
                """{"type":"keyDown","key":"f","code":"KeyF","windowsVirtualKeyCode":70,"text":"f"}""");
            await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent",
                """{"type":"keyUp","key":"f","code":"KeyF","windowsVirtualKeyCode":70}""");
            var layout = await EvalAsync(LayoutJs);
            Program.Log($"fullscreen attempt {attempt}: {layout} at {sw.ElapsedMilliseconds}ms");
            if (layout?.GetString() == "ok") break;
            if (layout?.GetString() == "broken")
            {
                retrying = true;
                await EvalAsync("document.exitFullscreen()");
            }
            await Task.Delay(1000);
        }
        retrying = false;
        // Load the watch list now, after the PIP settled, so it opens at once.
        // ponytail: ~350 MB more while the PIP is up even if the list never opens; load on first open again if that matters.
        if (pip) _ = InitListAsync();
    }

    void SendMenuState()
    {
        var m = mode switch { Mode.Pip => "pip", Mode.Music => "music", _ => "none" };
        var json = JsonSerializer.Serialize(new { mode = m, list = listOpen, onTop = state.OnTop, comments = cmtSide ?? cmtPending?.Side });
        web.CoreWebView2?.PostWebMessageAsJson(json);
        music.CoreWebView2?.PostWebMessageAsJson(json);
    }

    void ToggleOnTop()
    {
        TopMost = state.OnTop = !state.OnTop;
        state.Save();
        SendMenuState();
    }

    void ToggleList()
    {
        listOpen = !listOpen;
        if (listOpen) ShowList();
        else if (list.Visible)
        {
            var r = WithoutList(Bounds);
            r.Offset(-listShift.X, -listShift.Y);
            var wa = Work;
            r.X = Math.Clamp(r.X, wa.Left, Math.Max(wa.Left, wa.Right - r.Width));
            listShift = Point.Empty;
            // one layout after both, or the video gets the whole wide window for a moment (jumps right and back)
            SuspendLayout();
            list.Visible = false;
            Bounds = r;
            ResumeLayout();
        }
        SendMenuState();
    }

    // Grows the PIP by the panel so the video keeps its size and place, on whichever side the screen has room
    // (PlaceList); with no room anywhere it goes right and the video slides left, back when the panel closes.
    // ponytail: panel size is set when it opens (60% of the video's width, at least 360); resizing the PIP later keeps it.
    void ShowList()
    {
        if (mode != Mode.Pip || !listOpen || list.Visible) return;
        int size = Math.Max(LogicalToDeviceUnits(360), ClientSize.Width * 3 / 5);
        var (dock, bounds) = PlaceList(Bounds, size, Work);
        list.Dock = dock;
        if (dock is DockStyle.Left or DockStyle.Right) list.Width = size; else list.Height = size;
        var before = Location;
        SuspendLayout();  // as in ToggleList: else the video shrinks to what the panel leaves of the old size first
        list.Visible = true;
        Bounds = bounds;
        ResumeLayout();
        listShift = WithoutList(Bounds).Location - (Size)before;
        _ = InitListAsync();
    }

    // Right, left, below, above (not for comments: beside only): the first side the screen has room for the panel on; else
    // right, slid onto the screen.
    internal static (DockStyle, Rectangle) PlaceList(Rectangle r, int size, Rectangle work, bool besideOnly = false)
    {
        if (r.Right + size <= work.Right) return (DockStyle.Right, r with { Width = r.Width + size });
        if (r.Left - size >= work.Left) return (DockStyle.Left, Rectangle.FromLTRB(r.Left - size, r.Top, r.Right, r.Bottom));
        if (!besideOnly && r.Bottom + size <= work.Bottom) return (DockStyle.Bottom, r with { Height = r.Height + size });
        if (!besideOnly && r.Top - size >= work.Top) return (DockStyle.Top, Rectangle.FromLTRB(r.Left, r.Top - size, r.Right, r.Bottom));
        return (DockStyle.Right, new Rectangle(Math.Max(work.Left, work.Right - r.Width - size), r.Top, r.Width + size, r.Height));
    }

    // A Short's comments beside it, in the page (PlayStateJs's style), the window widened for them as for the list:
    // the video keeps its size and place unless the screen has no room, and comes back when they close.
    string? cmtSide;  // "left" / "right" while they are beside the video
    (string Side, Rectangle Bounds)? cmtPending;  // picked; the page lays out for it, then asks for the room (panel:placed)
    Point cmtShift;
    int CmtWidth => LogicalToDeviceUnits(360);  // the style's 360 CSS px

    void ShowComments()
    {
        if (!pip || !OnShorts || cmtSide != null || cmtPending != null) return;
        var (dock, bounds) = PlaceList(Bounds, CmtWidth, Work, besideOnly: true);
        cmtPending = (dock == DockStyle.Left ? "left" : "right", bounds);
        SendMenuState();
    }

    void PlaceComments()
    {
        if (cmtPending is not { } p) return;
        cmtPending = null;
        cmtSide = p.Side;
        var before = Location;
        if (p.Bounds.Left != Left) ResizeCovered(p.Bounds); else Bounds = p.Bounds;
        cmtShift = WithoutComments(Bounds).Location - (Size)before;
    }

    // The window's left edge moving (comments on the left): the page's last frame sits at the new edge, the video 360 px
    // off its place, until the page paints at the new size (some 50 ms, both ways, 2026-10-10). The PIP as it was, where
    // it was on the screen, over it until then; the rest the window's colour, as the new room on the right shows.
    async void ResizeCovered(Rectangle to)
    {
        using var now = new Bitmap(ClientSize.Width, ClientSize.Height);
        using (var g = Graphics.FromImage(now)) { var dc = g.GetHdc(); PrintWindow(Handle, dc, 3); g.ReleaseHdc(dc); }  // client, page included
        var shot = new Bitmap(to.Width - (Width - ClientSize.Width), to.Height - (Height - ClientSize.Height));
        using (var g = Graphics.FromImage(shot)) { g.Clear(BackColor); g.DrawImageUnscaled(now, Left - to.Left, 0); }
        using var cover = new PictureBox { Image = shot, Bounds = new Rectangle(Point.Empty, shot.Size) };
        Controls.Add(cover);
        cover.BringToFront();
        Bounds = to;
        await EvalAsync($"new Promise(r => {{ const t0 = Date.now(), f = () => Math.abs(innerWidth * devicePixelRatio - {web.Width}) < 2 || Date.now() - t0 > 1000"
            + " ? requestAnimationFrame(() => requestAnimationFrame(r)) : requestAnimationFrame(f); f(); })");
        shot.Dispose();
    }

    void HideComments()
    {
        if (cmtPending != null) { cmtPending = null; SendMenuState(); return; }
        if (cmtSide == null) return;
        var r = WithoutComments(Bounds);
        r.Offset(-cmtShift.X, -cmtShift.Y);
        var wa = Work;
        r.X = Math.Clamp(r.X, wa.Left, Math.Max(wa.Left, wa.Right - r.Width));
        cmtSide = null;
        cmtShift = Point.Empty;
        SendMenuState();
        if (r.Left != Left) ResizeCovered(r); else Bounds = r;
    }

    Rectangle WithoutComments(Rectangle b) => cmtSide switch
    {
        "left" => Rectangle.FromLTRB(b.Left + CmtWidth, b.Top, b.Right, b.Bottom),
        "right" => b with { Width = b.Width - CmtWidth },
        _ => b,
    };

    // The window without the list and the comments: where the video is.
    Rectangle PipOnly(Rectangle b) => WithoutComments(WithoutList(b));

    Rectangle WithoutList(Rectangle b) => !list.Visible ? b : list.Dock switch
    {
        DockStyle.Left => Rectangle.FromLTRB(b.Left + list.Width, b.Top, b.Right, b.Bottom),
        DockStyle.Top => Rectangle.FromLTRB(b.Left, b.Top + list.Height, b.Right, b.Bottom),
        DockStyle.Bottom => b with { Height = b.Height - list.Height },
        _ => b with { Width = b.Width - list.Width },
    };

    // Once: from ShowList, or ahead of it after the PIP's fullscreen (the home page takes ~2 s to load).
    Task InitListAsync() => listInit ??= InitListCoreAsync();
    Task? listInit;
    string? listFrom;  // the list panel's page before a watch page opened in it

    async Task InitListCoreAsync()
    {
        await list.EnsureCoreWebView2Async(env);
        await list.CoreWebView2!.AddScriptToExecuteOnDocumentCreatedAsync(ListJs);
        list.CoreWebView2.WebMessageReceived += async (_, e) =>
        {
            var msg = e.TryGetWebMessageAsString();
            if (msg == "close") { if (listOpen) ToggleList(); }
            else if (msg?.Split(':') is ["playlist", var pl, var vid])
            {
                // already playing from it: going to the playlist is not "play it from the start" (2026-10-10)
                if (mode == Mode.Pip && Program.WatchParam(web.CoreWebView2.Source, "list") == pl) Program.Log($"list playlist {pl}: playing it already");
                else await PlayFromListAsync($"https://www.youtube.com/watch?v={vid}&list={pl}");
            }
            else await PlayFromListAsync(msg);
        };
        // A video opened in the panel some way other than a link click (signed in, 2026-10-05; likely the hover
        // preview, which signed out never shows): back to the list, the panel's player stopped, and into the PIP.
        list.CoreWebView2.SourceChanged += async (_, _) =>
        {
            var url = list.CoreWebView2.Source;
            var from = listFrom;
            if (!Program.IsWatchUrl(url)) { listFrom = url; return; }
            // Signed in, ≡'s playlist entries open the playlist on a watch page, resuming it (2026-10-10: another video
            // played each time the user went back to a playlist). The playlist instead, in place of that page. From the
            // playlist's own page it plays, as its shuffle button may (no link to catch there).
            if (Program.WatchParam(url, "list") is { } pl && !pl.StartsWith("RD") && System.Text.RegularExpressions.Regex.IsMatch(pl, "^[A-Za-z0-9_-]+$")
                && !System.Text.RegularExpressions.Regex.IsMatch(from ?? "", $"[?&]list={pl}(&|$)"))
            {
                Program.Log($"list opened {url}: playlist shown");
                await list.CoreWebView2.ExecuteScriptAsync($"location.replace('/playlist?list={pl}')");
                return;
            }
            Program.Log($"list opened {url}");
            await list.CoreWebView2.ExecuteScriptAsync(ListBackJs);
            await PlayFromListAsync(url);
        };
        list.Source = new Uri("https://www.youtube.com/");
    }

    // Back once the watch page is up: going back while it is still opening is lost (the panel's first video,
    // 2026-10-05). stopVideo, not pause: the hidden miniplayer plays a paused video on after going back.
    const string ListBackJs = """
        (() => {
          const stop = () => document.querySelector('#movie_player')?.stopVideo?.();
          let n = 0;
          const t = setInterval(() => {
            stop();
            if (location.pathname === '/watch' && document.querySelector('ytd-watch-flexy:not([hidden])')) { clearInterval(t); history.back(); setTimeout(stop, 500); }
            else if (++n >= 50) clearInterval(t);
          }, 100);
        })()
        """;

    // YouTube's own in-page navigation, as when the next video starts in fullscreen: the player stays
    // fullscreen (0.2-0.4 s, measured 2026-10-05). A reload takes 5 s and the fullscreen retry.
    const string InPageWatchJs = """
        new Promise(done => {
          const id = $ID, list = $LIST, index = $INDEX;  // index: the link's, from 1; the endpoint's counts from 0
          document.querySelector('ytd-app').dispatchEvent(new CustomEvent('yt-navigate', { bubbles: true, composed: true, detail: { endpoint: {
            commandMetadata: { webCommandMetadata: { url: '/watch?v=' + id + (list ? '&list=' + list + (index ? '&index=' + index : '') : ''),
              webPageType: 'WEB_PAGE_TYPE_WATCH', rootVe: 3832 } },
            watchEndpoint: list ? { videoId: id, playlistId: list, ...(index && { index: index - 1 }) } : { videoId: id } } } }));
          const t0 = Date.now();
          const i = setInterval(() => {
            if (new URLSearchParams(location.search).get('v') === id) { clearInterval(i); done(!!document.fullscreenElement); }
            else if (Date.now() - t0 > 3000) { clearInterval(i); done(false); }
          }, 100);
        })
        """;

    // The same for a Short (0.3 s from the watch page, measured 2026-10-09); YouTube drops the watch page's fullscreen.
    const string InPageShortsJs = """
        new Promise(done => {
          const id = $ID;
          document.querySelector('ytd-app').dispatchEvent(new CustomEvent('yt-navigate', { bubbles: true, composed: true, detail: { endpoint: {
            commandMetadata: { webCommandMetadata: { url: '/shorts/' + id, webPageType: 'WEB_PAGE_TYPE_SHORTS', rootVe: 37414 } },
            reelWatchEndpoint: { videoId: id } } } }));
          const t0 = Date.now();
          const i = setInterval(() => {
            if (location.pathname === '/shorts/' + id) { clearInterval(i); done(true); }
            else if (Date.now() - t0 > 3000) { clearInterval(i); done(false); }
          }, 100);
        })
        """;

    async Task PlayFromListAsync(string? url)
    {
        if (Program.ShortsId(url) is { } sid)
        {
            if (mode != Mode.Pip) return;
            retrying = true;  // keeps the PIP while the watch page leaves fullscreen; SourceChanged clears it
            var r = await EvalAsync(InPageShortsJs.Replace("$ID", JsonSerializer.Serialize(sid)));
            Program.Log($"from list short {sid}: in-page {r}");
            if (r?.ValueKind != JsonValueKind.True) web.CoreWebView2.Navigate($"https://www.youtube.com/shorts/{sid}");
            return;
        }
        bool fromShorts = OnShorts;
        var id = Program.VideoId(url);
        if (mode != Mode.Pip || id == null || !System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Za-z0-9_-]+$")) return;
        var list = Program.WatchParam(url, "list") is { } l && System.Text.RegularExpressions.Regex.IsMatch(l, "^[A-Za-z0-9_-]+$") ? l : null;
        int index = int.TryParse(Program.WatchParam(url, "index"), out var i) && i > 0 && list != null ? i : 0;
        var inPage = await EvalAsync(InPageWatchJs.Replace("$ID", JsonSerializer.Serialize(id))
            .Replace("$LIST", JsonSerializer.Serialize(list)).Replace("$INDEX", index.ToString()));
        Program.Log($"from list {id} list={list} index={index}: in-page {inPage}");
        // from a Short there was no fullscreen to keep: SourceChanged presses it once the watch page is there
        if (inPage?.ValueKind == JsonValueKind.True || fromShorts && Program.VideoId(web.CoreWebView2.Source) == id) return;
        // fallback, if YouTube changes its internals: reload; `retrying` keeps the PIP while it is out of fullscreen
        lastVideo = null;
        retrying = true;
        web.CoreWebView2.Navigate($"https://www.youtube.com/watch?v={id}" + (list != null ? $"&list={list}" + (index > 0 ? $"&index={index}" : "") : ""));
    }

    // The video playing, where and how loud, in that view (YouTube's by default). Null when the page went away under it.
    async Task<(string? v, int t, int vol, bool muted)?> ReadPlayerAsync(CoreWebView2? c = null)
    {
        // In Music the track plays on while browsing its other pages (no ?v= then): ask the player. Not on
        // YouTube, whose watch player stays (paused) in the page after going back to the home page.
        if (await EvalAsync("""
            (p => ({ v: (location.host === 'music.youtube.com' && p?.getVideoData?.()?.video_id) || new URLSearchParams(location.search).get('v')
                       || location.pathname.match(/^\/shorts\/([\w-]+)/)?.[1],
                     t: Math.floor((p?.querySelector('video') ?? document.querySelector('video'))?.currentTime || 0),
                     vol: p?.getVolume ? Math.round(p.getVolume()) : 100, muted: !!p?.isMuted?.() }))(document.querySelector(location.pathname.startsWith('/shorts/') ? '#shorts-player' : '#movie_player'))
            """, c) is not { } now) return null;
        string? v = now.TryGetProperty("v", out var pv) ? pv.GetString() : null;  // left out when undefined (no video)
        if (v != null && !System.Text.RegularExpressions.Regex.IsMatch(v, "^[A-Za-z0-9_-]+$")) v = null;
        return (v, now.GetProperty("t").GetInt32(), Math.Clamp(now.GetProperty("vol").GetInt32(), 0, 100), now.GetProperty("muted").GetBoolean());
    }

    static string Resume(string? v, int t, int vol, bool muted) => ResumeJs.Replace("$ID", JsonSerializer.Serialize(v))
        .Replace("$T", t.ToString()).Replace("$V", vol.ToString()).Replace("$M", muted ? "true" : "false");

    // From Music to a video YouTube had to reload for: its position and volume once the page has it.
    async Task RestoreAsync()
    {
        if (pendingRestore is not { } r) return;
        pendingRestore = null;
        Program.Log($"restore {r.v} t={r.t} vol={r.vol} muted={r.muted}: {(await EvalAsync(Resume(r.v, r.t, r.vol, r.muted)))?.GetString()}");
    }

    // Same video and position, in YouTube Music (a plain small window, no fullscreen there) or back in the YouTube PIP.
    async Task ToggleMusicAsync()
    {
        if (mode == Mode.Music) { await LeaveMusicAsync(null); return; }
        var (v, t, vol, muted) = await ReadPlayerAsync() ?? (null, 0, 100, false);
        Program.Log($"music toggle from {mode}: v={v} t={t} vol={vol} muted={muted}");
        // from any mode: the video being watched, or Music as it is (its home the first time) when there is none
        await EnterMusicAsync(v != null ? $"https://music.youtube.com/watch?v={v}" : null, v, t, vol, muted);
    }

    // Music shows at once and YouTube plays on until Music does, then stops: no gap in the sound. The track loads only if
    // Music has another. url: what to open, null for Music as it is; v: the track in it, to take position t and volume vol.
    async Task EnterMusicAsync(string? url, string? v, int t, int vol, bool muted)
    {
        if (mode == Mode.Music) return;
        var gen = ++switches;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var m = await MusicAsync();
        if (gen != switches) return;  // pressed again while Music's view was being made
        backMode = mode;
        SetMode(Mode.Music);
        bool fresh = m.Source == "about:blank";
        var now = fresh ? null : await ReadPlayerAsync(m);
        bool load = url != null && (v == null || !SameTrack(now?.v, v)) || url == null && fresh;
        if (load)
        {
            // asked from the new page on: the old one has another track, and Music may play the track under another id
            // (its song for the video: asked rrYkyw9O6cg, played Zf1d8SGuxfs, 2026-10-10), so not waited for by id
            var loading = new TaskCompletionSource();
            void Loading(object? s, CoreWebView2ContentLoadingEventArgs e) { m.ContentLoading -= Loading; loading.TrySetResult(); }
            m.ContentLoading += Loading;
            m.Navigate(url ?? "https://music.youtube.com/");
            await Task.WhenAny(loading.Task, Task.Delay(10000));
        }
        string? r = null;
        // cut off if the page goes again (a redirect), then asked again
        while (v != null && r == null && gen == switches && sw.ElapsedMilliseconds < 20000)
            if ((r = (await EvalAsync(Resume(load ? null : now?.v, t, vol, muted), m))?.GetString()) == null) await Task.Delay(50);
        HandOver(gen, Mode.Music, m, web.CoreWebView2);
        if (v != null && gen == switches && r?.StartsWith("playing") == true && (await ReadPlayerAsync(m))?.v is { } got) musicTrack = (v, got);
        Program.Log($"into Music {v} ({(load ? "loaded" : "as it was")}): {r ?? "nothing to wait for"}; handed over at {sw.ElapsedMilliseconds}ms, Music plays {musicTrack?.Got}");
    }

    // The same track on both sides, also when Music plays it under its own id (musicTrack).
    bool SameTrack(string? a, string? b) => SameTrack(a, b, musicTrack);

    internal static bool SameTrack(string? a, string? b, (string Asked, string Got)? music) => a != null && b != null
        && (a == b || music is { } m && (m.Asked == a && m.Got == b || m.Asked == b && m.Got == a));

    // YouTube shows at once and Music plays on until YouTube does, then stops. The same video: the page as it was left, at
    // Music's position. Another: YouTube's in-page navigation, as from the list (a reload only if that fails).
    // url: a YouTube page Music linked to, opened in the big window.
    async Task LeaveMusicAsync(string? url)
    {
        if (mode != Mode.Music) return;
        var gen = ++switches;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var m = music.CoreWebView2;
        if (url != null)
        {
            Program.Log($"out of Music to {url}");
            SetMode(Mode.Browse);
            web.CoreWebView2.Navigate(url);
            HandOver(gen, Mode.Browse, web.CoreWebView2, m);
            return;
        }
        var (v, t, vol, muted) = await ReadPlayerAsync(m) ?? (null, 0, 100, false);
        if (gen != switches) return;
        var src = web.CoreWebView2.Source;
        var ytId = Program.VideoId(src) ?? Program.ShortsId(src);
        bool same = SameTrack(v, ytId);
        Program.Log($"out of Music: v={v} t={t} vol={vol} muted={muted}, YouTube {(same ? "has it" : "at " + src)}, left in {backMode}");
        string? r = null;
        if (v == null || same)
        {
            SetMode(backMode);
            // no track in Music: the video as it was left, playing again if it was in the PIP
            if (v != null || backMode != Mode.Browse) r = (await EvalAsync(v != null ? Resume(ytId, t, vol, muted) : Resume(null, -1, -1, false)))?.GetString();
        }
        else
        {
            retrying = true;  // keeps the PIP while it is not fullscreen; SourceChanged covers it and presses fullscreen
            SetMode(monitorFull ? Mode.Monitor : Mode.Pip);
            await EvalAsync(InPageWatchJs.Replace("$ID", JsonSerializer.Serialize(v)).Replace("$LIST", "null").Replace("$INDEX", "0"));
            if ((await EvalAsync("new URLSearchParams(location.search).get('v')"))?.ToString() == v)
                r = (await EvalAsync(Resume(v, t, vol, muted)))?.GetString();
            else
            {
                pendingRestore = (v, t, vol, muted);
                lastVideo = null;
                web.CoreWebView2.Navigate($"https://www.youtube.com/watch?v={v}");
            }
        }
        HandOver(gen, Mode.Browse, web.CoreWebView2, m);
        Program.Log($"out of Music: {r ?? "nothing to wait for"}; handed over at {sw.ElapsedMilliseconds}ms");
    }

    // After the side switched to plays (or never will): the other stops. Pressed again meanwhile, whichever side is not
    // showing now stops instead (the one this switch was starting, if the window went back). Muted here at once: a hidden
    // view's page runs the pause only when shown again (installed, 2026-10-10: YouTube played on 23 s under Music), so it
    // is not waited for either. SetMode unmutes the side it shows; the late pause still lands before that side's Resume.
    void HandOver(int gen, Mode to, CoreWebView2 shown, CoreWebView2 other)
    {
        var off = gen == switches ? other : (mode == Mode.Music) != (to == Mode.Music) ? shown : null;
        if (off == null) return;
        off.IsMuted = true;
        _ = EvalAsync(PauseJs, off);
    }

    async Task<JsonElement?> EvalAsync(string js, CoreWebView2? c = null)
    {
        var args = JsonSerializer.Serialize(new { expression = js, awaitPromise = true, returnByValue = true });
        string result;
        // The page went away under it (on a fresh profile YouTube reloads the first watch page): no value.
        try { result = await (c ?? web.CoreWebView2).CallDevToolsProtocolMethodAsync("Runtime.evaluate", args); }
        catch (ArgumentException) { Program.Log("eval cut off: page reloaded"); return null; }
        return JsonDocument.Parse(result).RootElement.GetProperty("result").TryGetProperty("value", out var v) ? v.Clone() : null;
    }

    // Chromium holds the mouse capture, so Windows' own move loop cannot take over: follow the
    // cursor until the left button is released.
    void StartDrag()
    {
        if (dragTimer.Enabled) return;
        dragOffset = new Size(Cursor.Position.X - Left, Cursor.Position.Y - Top);
        dragTimer.Start();
    }

    void DragTick()
    {
        const int VK_LBUTTON = 1;
        if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0) { dragTimer.Stop(); return; }
        Location = Cursor.Position - dragOffset;
    }

    void RememberRect()
    {
        if (mode == Mode.Pip) { var r = PipOnly(Bounds); r.Offset(-listShift.X - cmtShift.X, -listShift.Y - cmtShift.Y); state.PipRect = r; }
        else if (mode == Mode.Browse) state.BrowseRect = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
    }

    void SetMode(Mode next)
    {
        if (next == mode) return;
        RememberRect();
        WindowState = FormWindowState.Normal;
        mode = next;
        web.Visible = next != Mode.Music;
        music.Visible = next == Mode.Music;
        if ((next == Mode.Music ? music : web).CoreWebView2 is { } shown) shown.IsMuted = false;  // muted by HandOver
        if (ContainsFocus) (next == Mode.Music ? music : web).Focus();  // the keys (space, arrows) to the side showing
        ShowTitle();
        list.Visible = false;  // the rects below are without the panel; ShowList adds it back in PIP
        if (next == Mode.Music) listOpen = false;  // not back with the PIP from Music: it opened again with it (2026-10-10)
        listShift = Point.Empty;
        cmtSide = null;
        cmtPending = null;
        cmtShift = Point.Empty;
        // Borderless over the whole monitor, so Windows treats it as fullscreen and the taskbar goes behind.
        FormBorderStyle = next == Mode.Monitor ? FormBorderStyle.None : FormBorderStyle.Sizable;
        ApplyFrame();
        // Below 615 CSS px YouTube Music turns into its mobile layout, and widening again leaves its top bar
        // hidden and the player bar gone until a reload (measured 2026-10-05). Keep Music above that.
        MinimumSize = next == Mode.Music ? new Size(LogicalToDeviceUnits(620) + Width - ClientSize.Width, 0) : Size.Empty;
        Bounds = next switch
        {
            Mode.Monitor => Screen.FromControl(this).Bounds,
            Mode.Pip or Mode.Music => state.PipRect,
            _ => state.BrowseRect,
        };
        // Windows guesses fullscreen from a window over the whole monitor, and missed it here: the taskbar stayed over
        // the video (2026-10-10). Telling the taskbar, as Chromium does, sends it behind while this window is active.
        Taskbar.MarkFullscreenWindow(Handle, next == Mode.Monitor);
        if (next == Mode.Pip) { ShowList(); FitAspect(); }
        SendMenuState();
    }

    static readonly ITaskbarList2 Taskbar = NewTaskbar();
    static ITaskbarList2 NewTaskbar() { var t = (ITaskbarList2)new TaskbarList(); t.HrInit(); return t; }

    [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090")] class TaskbarList { }

    [ComImport, Guid("602D4995-B13A-429b-A66E-1935E44F4317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList2
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
    }

    // Re-runs WM_NCCALCSIZE so the title bar hides/returns.
    void ApplyFrame()
    {
        const uint SWP_FRAMECHANGED = 0x20, SWP_NOMOVE = 2, SWP_NOSIZE = 1, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10;
        SetWindowPos(Handle, 0, 0, 0, 0, 0, SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    // Everything in the PIP window that is not video while it plays: the frame. The title bar is hidden
    // then, leaving only the bottom frame (half of left + right); the playing message can arrive after
    // the resize, so do not measure the current height.
    Size Chrome
    {
        get
        {
            int frame = Width - ClientSize.Width;
            bool beside = list.Dock is DockStyle.Left or DockStyle.Right;
            return new(frame + (list.Visible && beside ? list.Width : 0) + (cmtSide != null ? CmtWidth : 0), frame / 2 + (list.Visible && !beside ? list.Height : 0));
        }
    }

    void FitAspect() => Bounds = Refit(Bounds, Chrome, Aspect, Work);

    // The test copy sits off every screen and must stay there: no screen to keep it on. YTPIP_TEST_EDGE=1: the screen
    // ends at its right edge, as a PIP in the bottom-right corner (panels open on the left).
    Rectangle Work => !Program.Test ? Screen.FromControl(this).WorkingArea
        : TestEdge ? Rectangle.FromLTRB(Left - (1 << 20), Top, Right, Top + (1 << 20)) : new Rectangle(Left, Top, 1 << 20, 1 << 20);
    static readonly bool TestEdge = Program.Test && Environment.GetEnvironmentVariable("YTPIP_TEST_EDGE") == "1";

    // The same video area in the new shape (a 512x288 PIP becomes about 288x512 for a Short), its top-left corner
    // where it was, moved only to stay on the screen. (It kept the nearest screen corner, which pinned a PIP in the
    // lower half by its bottom edge; the user asked for top-left, 2026-10-10.)
    internal static Rectangle Refit(Rectangle r, Size chrome, double aspect, Rectangle work)
    {
        double area = Math.Max(1, r.Width - chrome.Width) * (double)Math.Max(1, r.Height - chrome.Height);
        var fit = LockAspect(new Rectangle(0, 0, (int)Math.Round(Math.Sqrt(area * aspect)) + chrome.Width, 0), 8, chrome, aspect);
        if (fit.Height > work.Height) fit = LockAspect(new Rectangle(0, 0, 0, work.Height), 6, chrome, aspect);
        int x = Math.Clamp(r.Left, work.Left, Math.Max(work.Left, work.Right - fit.Width));
        int y = Math.Clamp(r.Top, work.Top, Math.Max(work.Top, work.Bottom - fit.Height));
        return new Rectangle(x, y, fit.Width, fit.Height);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        const int MF_SEPARATOR = 0x800;
        var menu = GetSystemMenu(Handle, false);
        AppendMenu(menu, MF_SEPARATOR, 0, null);
        AppendMenu(menu, 0, IdOnTop, "항상 위에 표시");
        AppendMenu(menu, 0, IdMonitorFull, "전체화면을 모니터 전체로 (PIP 끔)");
        AppendMenu(menu, 0, IdMusic, "YouTube Music으로 전환");
        AppendMenu(menu, 0, IdList, "볼 목록 열기");
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCCALCSIZE = 0x83, WM_SIZING = 0x214, WM_SYSCOMMAND = 0x112, WM_INITMENU = 0x116, MF_CHECKED = 8, MF_GRAYED = 1;
        switch (m.Msg)
        {
            case WM_NCCALCSIZE when TitleHidden && m.WParam != 0:
            {
                // Let Windows lay out the normal frame, then give the title bar to the client.
                int top = Marshal.ReadInt32(m.LParam, 4);
                base.WndProc(ref m);
                Marshal.WriteInt32(m.LParam, 4, top);
                return;
            }
            case WM_SIZING when pip:
            {
                var r = Marshal.PtrToStructure<RECT>(m.LParam);
                var fixedRect = LockAspect(Rectangle.FromLTRB(r.L, r.T, r.R, r.B), (int)m.WParam, Chrome, Aspect);
                Marshal.StructureToPtr(new RECT(fixedRect), m.LParam, false);
                m.Result = 1;
                return;
            }
            case WM_INITMENU:
            {
                var menu = GetSystemMenu(Handle, false);
                CheckMenuItem(menu, IdOnTop, state.OnTop ? MF_CHECKED : 0);
                CheckMenuItem(menu, IdMonitorFull, monitorFull ? MF_CHECKED : 0);
                ModifyMenu(menu, IdMusic, 0, IdMusic, mode == Mode.Music ? "YouTube로 전환" : "YouTube Music으로 전환");
                // the list sits beside the PIP only (never in Music)
                ModifyMenu(menu, IdList, mode == Mode.Pip ? 0 : MF_GRAYED, IdList, listOpen ? "볼 목록 닫기" : "볼 목록 열기");
                break;
            }
            case WM_SYSCOMMAND when ((int)m.WParam & 0xFFF0) == IdList:
                ToggleList();
                return;
            case WM_SYSCOMMAND when ((int)m.WParam & 0xFFF0) == IdMusic:
                _ = ToggleMusicAsync();
                return;
            case WM_SYSCOMMAND when ((int)m.WParam & 0xFFF0) == IdOnTop:
                ToggleOnTop();
                return;
            case WM_SYSCOMMAND when ((int)m.WParam & 0xFFF0) == IdMonitorFull:
                monitorFull = !monitorFull;
                if (mode is Mode.Pip or Mode.Monitor) SetMode(monitorFull ? Mode.Monitor : Mode.Pip);
                return;
        }
        base.WndProc(ref m);
    }

    // Keeps the video area at the video's shape (width / height) while resizing; the edge being dragged decides which side moves.
    internal static Rectangle LockAspect(Rectangle r, int edge, Size chrome, double aspect)
    {
        // the shorter side at least 135 (240x135 at 16:9)
        int minW = (int)Math.Round(aspect >= 1 ? 135 * aspect : 135), minH = (int)Math.Round(aspect >= 1 ? 135 : 135 / aspect);
        int w, h;
        if (edge is 3 or 6)  // WMSZ_TOP, WMSZ_BOTTOM: height leads
        {
            h = Math.Max(r.Height, minH + chrome.Height);
            w = (int)Math.Round((h - chrome.Height) * aspect) + chrome.Width;
        }
        else
        {
            w = Math.Max(r.Width, minW + chrome.Width);
            h = (int)Math.Round((w - chrome.Width) / aspect) + chrome.Height;
        }
        bool fromLeft = edge is 1 or 4 or 7;  // WMSZ_LEFT, TOPLEFT, BOTTOMLEFT
        bool fromTop = edge is 3 or 4 or 5;   // WMSZ_TOP, TOPLEFT, TOPRIGHT
        int x = fromLeft ? r.Right - w : r.Left;
        int y = fromTop ? r.Bottom - h : r.Top;
        return new Rectangle(x, y, w, h);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int L, T, R, B;
        public RECT(Rectangle r) { L = r.Left; T = r.Top; R = r.Right; B = r.Bottom; }
    }

    [DllImport("user32.dll")] static extern IntPtr GetSystemMenu(IntPtr hWnd, bool revert);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenu(IntPtr menu, int flags, nint id, string? text);
    [DllImport("user32.dll")] static extern int CheckMenuItem(IntPtr menu, int id, int flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool ModifyMenu(IntPtr menu, int id, int flags, nint newId, string text);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hWnd, IntPtr dc, uint flags);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}

sealed class State(string file)
{
    public Rectangle BrowseRect, PipRect;
    public bool OnTop = true;

    public void Load()
    {
        var wa = Screen.PrimaryScreen!.WorkingArea;
        BrowseRect = new(wa.X + (wa.Width - 1100) / 2, wa.Y + (wa.Height - 750) / 2, 1100, 750);
        PipRect = new(wa.Right - 512 - 24, wa.Bottom - 288 - 24, 512, 288);
        if (Program.Test)
        {
            // left of every screen: ShowList only ever slides a window that runs off the right edge
            BrowseRect.X = PipRect.X = SystemInformation.VirtualScreen.Left - 3000;
            OnTop = false;
            return;
        }
        try
        {
            var lines = File.ReadAllLines(file);
            if (lines.Length >= 2 && TryParseRect(lines[0], out var b) && TryParseRect(lines[1], out var p))
                (BrowseRect, PipRect) = (b, p);
            if (lines.Length >= 3)
            {
                OnTop = !lines[2].Contains("ontop=0");
            }
        }
        catch (IOException) { }  // first run: keep defaults
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllLines(file,
            [Format(BrowseRect), Format(PipRect), $"ontop={(OnTop ? 1 : 0)}"]);
    }

    static string Format(Rectangle r) => $"{r.X},{r.Y},{r.Width},{r.Height}";

    // Rejects garbage and rects that are off every screen (e.g. a monitor got unplugged).
    internal static bool TryParseRect(string s, out Rectangle r)
    {
        r = default;
        var parts = s.Split(',');
        if (parts.Length != 4) return false;
        var n = new int[4];
        for (int i = 0; i < 4; i++)
            if (!int.TryParse(parts[i], out n[i])) return false;
        if (n[2] < 200 || n[3] < 120) return false;
        r = new Rectangle(n[0], n[1], n[2], n[3]);
        var rr = r;
        return Screen.AllScreens.Any(sc => sc.WorkingArea.IntersectsWith(rr));
    }
}

static class SelfTest
{
    public static int Run()
    {
        var chrome = new Size(16, 8);  // e.g. 8px resize borders left/right/bottom, none on top
        // Dragging the bottom-right corner: width leads, top-left stays.
        const double wide = 16.0 / 9, tall = 9.0 / 16;
        var a = PipForm.LockAspect(new Rectangle(100, 100, 656, 999), 8, chrome, wide);
        Check(a == new Rectangle(100, 100, 656, 368), $"bottom-right {a}");
        // Dragging the top-left corner keeps the bottom-right fixed.
        var b = PipForm.LockAspect(Rectangle.FromLTRB(0, 0, 656, 500), 4, chrome, wide);
        Check(b.Right == 656 && b.Bottom == 500 && b.Height == 368, $"top-left {b}");
        // Dragging the bottom edge: height leads.
        var c = PipForm.LockAspect(new Rectangle(0, 0, 999, 368), 6, chrome, wide);
        Check(c.Width == 656 && c.Height == 368, $"bottom edge {c}");
        // Minimum size: the shorter side 135 either way round.
        Check(PipForm.LockAspect(new Rectangle(0, 0, 10, 10), 8, chrome, wide).Width == 256, "min width");
        Check(PipForm.LockAspect(new Rectangle(0, 0, 10, 10), 8, chrome, tall).Width == 151, "min width, tall");

        // Music playing a video under its own id is still that video, either way round; nothing is the same as nothing.
        Check(PipForm.SameTrack("a", "a", null), "same id");
        Check(PipForm.SameTrack("b", "a", ("a", "b")) && PipForm.SameTrack("a", "b", ("a", "b")), "music's id");
        Check(!PipForm.SameTrack("c", "a", ("a", "b")), "another track");
        Check(!PipForm.SameTrack(null, null, null), "no track");

        // A Short in a PIP: same area standing up, the top-left corner kept; and back.
        var work = new Rectangle(0, 0, 1920, 1040);
        var pip = new Rectangle(600, 500, 512, 288);
        var s1 = PipForm.Refit(pip, chrome, tall, work);
        Check(s1.Location == pip.Location && s1.Width == 295 && s1.Height == 504, $"to tall {s1}");
        var s2 = PipForm.Refit(s1, chrome, wide, work);
        Check(s2.Location == pip.Location && Math.Abs(s2.Width - pip.Width) <= 2, $"back to wide {s2}");
        // Standing up at the bottom-right it moves up only as far as the screen needs.
        var s4 = PipForm.Refit(new Rectangle(1384, 728, 512, 288), chrome, tall, work);
        Check(s4.X == 1384 && s4.Bottom == 1040, $"kept on screen {s4}");
        // At the top-left it stays there; too tall for the screen it is cut to the screen's height.
        var s3 = PipForm.Refit(new Rectangle(10, 10, 1600, 900), chrome, tall, work);
        Check(s3.X == 10 && s3.Y == 0 && s3.Height == 1040, $"capped {s3}");

        // The watch list goes on the first side with room, the video staying put; with none, right and slid left.
        Check(PipForm.PlaceList(new Rectangle(24, 24, 512, 288), 360, work) == (DockStyle.Right, new Rectangle(24, 24, 872, 288)), "list right");
        Check(PipForm.PlaceList(new Rectangle(1384, 728, 512, 288), 360, work) == (DockStyle.Left, new Rectangle(1024, 728, 872, 288)), "list left");
        Check(PipForm.PlaceList(new Rectangle(100, 100, 1700, 300), 360, work) == (DockStyle.Bottom, new Rectangle(100, 100, 1700, 660)), "list below");
        Check(PipForm.PlaceList(new Rectangle(100, 700, 1700, 300), 360, work) == (DockStyle.Top, new Rectangle(100, 340, 1700, 660)), "list above");
        Check(PipForm.PlaceList(new Rectangle(100, 200, 1700, 700), 360, work) == (DockStyle.Right, new Rectangle(0, 200, 2060, 700)), "list slid");
        // Comments go beside only: room below is not taken.
        Check(PipForm.PlaceList(new Rectangle(100, 100, 1700, 300), 360, work, besideOnly: true) == (DockStyle.Right, new Rectangle(0, 100, 2060, 300)), "comments beside");

        using (var s = typeof(PipForm).Assembly.GetManifestResourceStream("app.ico"))
            Check(s != null && new Icon(s, 16, 16).Width == 16, "embedded icon");
        Check(!State.TryParseRect("1,2,3", out _), "too few parts");
        Check(!State.TryParseRect("a,2,300,200", out _), "not a number");
        Check(!State.TryParseRect("0,0,50,50", out _), "too small");
        Check(!State.TryParseRect("-99999,-99999,300,200", out _), "off screen");
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Check(State.TryParseRect($"{wa.X + 10},{wa.Y + 10},500,300", out _), "valid");

        Check(Program.VideoId("https://www.youtube.com/watch?v=abc&t=5") == "abc", "watch id");
        Check(Program.WatchParam("https://www.youtube.com/watch?v=abc&list=PLx&index=3", "list") == "PLx", "watch list");
        Check(Program.WatchParam("https://www.youtube.com/watch?v=abc&playlist=PLx", "list") == null, "not list");
        Check(Program.VideoId("https://www.youtube.com/watch?list=x&v=abc") == "abc", "id not first");
        Check(Program.VideoId("https://evil.example/watch?v=abc") == null, "other host");
        Check(Program.VideoId("http://www.youtube.com/watch?v=abc") == null, "not https");
        Check(Program.VideoId("https://www.youtube.com/results?search_query=v=x") == null, "not watch");
        Check(Program.VideoId("https://www.youtube.com/watch?v=") == null, "empty id");
        Check(Program.VideoId(null) == null, "null");
        Check(Program.ShortsId("https://www.youtube.com/shorts/t2Pl9D-mhiI") == "t2Pl9D-mhiI", "shorts id");
        Check(Program.ShortsId("https://www.youtube.com/shorts/") == null, "shorts tab");
        Check(Program.ShortsId("https://www.youtube.com/shorts/a'b") == null, "shorts id chars");
        Check(Program.ShortsId("https://evil.example/shorts/abc") == null, "shorts other host");
        Check(Program.ShortsId("https://www.youtube.com/watch?v=abc") == null, "watch not shorts");
        return failed ? 1 : 0;
    }

    static bool failed;
    static void Check(bool ok, string what)
    {
        if (ok) return;
        failed = true;
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "yt-pip-selftest.log"), $"FAIL {what}\n");
    }
}
