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
            if (e.target.closest && e.target.closest('#movie_player') && !(onBar && msg === 'paused')) chrome.webview.postMessage(msg);
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
        // A Short (upright, at most 3 minutes) loops as on YouTube's own Shorts page, instead of autoplay going on to
        // the next video: it plays here as a watch page. Never an ad's: it would play forever.
        document.addEventListener('loadedmetadata', e => {
          const v = e.target;
          if (location.host === 'www.youtube.com' && v.closest?.('#movie_player'))
            v.loop = v.videoHeight > v.videoWidth && v.duration <= 181 && !document.querySelector('#movie_player.ad-showing');
        }, true);
        // Where the video is, for the list panel's thumbnail bars: YouTube draws its own once, when the panel loads.
        let progress = '';
        document.addEventListener('timeupdate', e => {
          const v = e.target, id = new URLSearchParams(location.search).get('v');
          if (location.host !== 'www.youtube.com' || !id || !v.closest?.('#movie_player') || !v.duration || document.querySelector('#movie_player.ad-showing')) return;
          const m = 'progress:' + id + ':' + Math.round(v.currentTime / v.duration * 100);
          if (m !== progress) chrome.webview.postMessage(progress = m);
        }, true);
        // Product cards over the video ("featured product" is a suggested-action badge in the player script), the Shorts
        // "제품 보기" pill (with its row) and the product panel it opens (2026-10-05).
        document.addEventListener('DOMContentLoaded', () => document.head.append(Object.assign(document.createElement('style'), {
          textContent: '.ytp-suggested-action, .ytp-featured-product, yt-shorts-suggested-action-view-model, .ytReelMetapanelViewModelMetapanelItem:has(> yt-shorts-suggested-action-view-model), ytd-engagement-panel-section-list-renderer[target-id^=shopping_panel] { display: none !important; }'
            // The fullscreen playlist button narrows the player for the playlist but leaves the panel `hidden` (any window
            // width, 2026-10-05; why is inside YouTube's script). ponytail: shown while the page says it is open.
            + 'ytd-watch-flexy[fullscreen][playlist-panel-expanded] #panels-full-bleed-container > ytd-playlist-panel-renderer[hidden] { display: flex !important; }' })));
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
            if (e.button !== 0 || !document.fullscreenElement) return;
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
          let state = { mode: 'none', list: false, onTop: false }, host, menu, musicItem, listItem, ctls;
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
            if (!host) return;
            const v = document.querySelector('#movie_player video') || document.querySelector('video');
            // in Music always: there it is the only way back besides the title bar menu
            const show = isMusic ? state.mode === 'music' : state.mode === 'pip' && v && v.paused && document.fullscreenElement;
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
          for (const t of ['play', 'playing', 'pause', 'ended']) document.addEventListener(t, update, true);
          document.addEventListener('fullscreenchange', update);
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
          const id = u.pathname.startsWith('/shorts/') ? u.pathname.split('/')[2] : u.searchParams.get('v');
          if (!id) return;
          const list = u.searchParams.get('list'), index = u.searchParams.get('index');
          chrome.webview.postMessage('https://www.youtube.com/watch?v=' + id + (list ? '&list=' + list + (index ? '&index=' + index : '') : ''));
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

    // After switching between YouTube and YouTube Music (separate sites, separate saved volume): carry the
    // volume over, and the position too into Music, which has no start-time link. Player API, since
    // video.volume is YouTube's loudness-normalised value.
    const string RestoreJs = """
        new Promise(done => {
          const t0 = Date.now();
          const i = setInterval(() => {
            const v = document.querySelector('video'), p = document.querySelector('#movie_player');
            if (v && v.readyState >= 1 && p && p.setVolume) {
              clearInterval(i);
              if ($T > 0) v.currentTime = $T;
              p.setVolume($V); $M ? p.mute() : p.unMute();
              done('ok vol ' + p.getVolume() + (p.isMuted() ? ' muted' : ''));
            }
            else if (Date.now() - t0 > 15000) { clearInterval(i); done('timeout'); }
          }, 200);
        })
        """;

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
            const ready = document.querySelector('ytd-watch-flexy')?.getAttribute('video-id') === new URLSearchParams(location.search).get('v');
            if (!ready && Date.now() - t0 < 5000) return;
            clearInterval(t);
            // F typed into a focused box (search, comment) would be text, not the key
            if (document.activeElement?.matches('input, textarea, [contenteditable]:not([contenteditable=false])')) document.activeElement.blur();
            done(true);
          }, 100);
          setTimeout(() => { clearInterval(t); done(null); }, 15000);
        })
        """;

    // While a new video gets to the PIP: the window's colour over the page. It lets the mouse through (and
    // elementFromPoint looks past it), so the fullscreen button is still found and pressed.
    const string CoverJs = """
        document.getElementById('yt-pip-cover') || document.documentElement.append(Object.assign(document.createElement('div'), { id: 'yt-pip-cover',
          style: 'position:fixed;inset:0;z-index:2147483647;background:#181818;pointer-events:none' }))
        """;
    const string UncoverJs = "document.getElementById('yt-pip-cover')?.remove()";

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
    readonly WebView2 list = new() { Dock = DockStyle.Right, Visible = false };  // "볼 목록" beside the PIP (side set in ShowList)
    CoreWebView2Environment? env;
    bool listOpen;  // session only, like monitorFull
    Point listShift;  // how far ShowList slid the video to fit the panel; closing slides it back
    (int t, int vol, bool muted)? pendingRestore;  // applied once the page switched to has its player
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
        ShowInTaskbar = !Program.Test;
        Controls.Add(web);
        Controls.Add(list);  // added after the Fill control so docking gives it the right edge first
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
        await core.AddScriptToExecuteOnDocumentCreatedAsync(PlayStateJs);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(DragJs);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(MenuJs);
        core.WebMessageReceived += async (_, e) =>
        {
            var msg = e.TryGetWebMessageAsString();
            switch (msg)
            {
                case "drag": if (pip) StartDrag(); return;
                case "menu:list": ToggleList(); return;
                case "menu:music": await ToggleMusicAsync(); return;
                case "menu:ontop": ToggleOnTop(); return;
                case not null when msg.StartsWith("progress:"): list.CoreWebView2?.PostWebMessageAsString(msg); return;
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
            // YouTube Music has no fullscreen; leaving YouTube for it drops fullscreen, which is not "go back to browsing".
            if (mode == Mode.Music || retrying && !core.ContainsFullScreenElement) return;
            SetMode(!core.ContainsFullScreenElement ? Mode.Browse : monitorFull ? Mode.Monitor : Mode.Pip);
            ShowList();  // back from Music the window was PIP already, so SetMode did nothing
        };
        // YouTube navigates in-page; a new video picked while browsing goes PIP once it plays
        // (not with monitor fullscreen on: then it behaves like the web player).
        core.SourceChanged += async (_, _) =>
        {
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
                retrying = true;  // keeps the PIP while it is not fullscreen yet
                SetMode(Mode.Pip);
                await EvalAsync(CoverJs);
                await EnterFullscreenAsync();
                if (!core.ContainsFullScreenElement && mode == Mode.Pip) SetMode(Mode.Browse);  // it never got there: the page as it is
                await EvalAsync(UncoverJs);
            }
            // `retrying` here: a video picked from the list or the way back from Music, the window is already PIP.
            else if (retrying) await EnterFullscreenAsync();
        };
        core.NavigationCompleted += async (_, _) =>
        {
            SendMenuState();
            if (pendingRestore is var (t, vol, muted))
            {
                pendingRestore = null;
                var js = RestoreJs.Replace("$T", t.ToString()).Replace("$V", vol.ToString()).Replace("$M", muted ? "true" : "false");
                Program.Log($"restore t={t} vol={vol} muted={muted}: {await EvalAsync(js)}");
            }
        };
        // YouTube Music asks "leave site?" on every navigation away, which stalls the switch back to YouTube.
        // Without the default dialogs, alert/confirm become message boxes.
        core.Settings.AreDefaultScriptDialogsEnabled = false;
        core.ScriptDialogOpening += (_, e) =>
        {
            if (e.Kind == CoreWebView2ScriptDialogKind.Beforeunload ||
                MessageBox.Show(this, e.Message, Text, e.Kind == CoreWebView2ScriptDialogKind.Alert ? MessageBoxButtons.OK : MessageBoxButtons.OKCancel) == DialogResult.OK)
                e.Accept();
        };
        core.DocumentTitleChanged += (_, _) => Text = core.DocumentTitle;
        web.Source = new Uri(startUrl ?? "https://www.youtube.com/");
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
        web.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(new { mode = m, list = listOpen, onTop = state.OnTop }));
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
            var r = PipOnly(Bounds);
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
        // On the way back from Music the window is PIP before the page is: the fullscreen that follows opens it.
        if (mode != Mode.Pip || !listOpen || list.Visible || web.Source?.Host == "music.youtube.com") return;
        int size = Math.Max(LogicalToDeviceUnits(360), ClientSize.Width * 3 / 5);
        var (dock, bounds) = PlaceList(Bounds, size, Work);
        list.Dock = dock;
        if (dock is DockStyle.Left or DockStyle.Right) list.Width = size; else list.Height = size;
        var before = Location;
        SuspendLayout();  // as in ToggleList: else the video shrinks to what the panel leaves of the old size first
        list.Visible = true;
        Bounds = bounds;
        ResumeLayout();
        listShift = PipOnly(Bounds).Location - (Size)before;
        _ = InitListAsync();
    }

    // Right, left, below, above: the first side the screen has room for the panel on; else right, slid onto the screen.
    internal static (DockStyle, Rectangle) PlaceList(Rectangle r, int size, Rectangle work)
    {
        if (r.Right + size <= work.Right) return (DockStyle.Right, r with { Width = r.Width + size });
        if (r.Left - size >= work.Left) return (DockStyle.Left, Rectangle.FromLTRB(r.Left - size, r.Top, r.Right, r.Bottom));
        if (r.Bottom + size <= work.Bottom) return (DockStyle.Bottom, r with { Height = r.Height + size });
        if (r.Top - size >= work.Top) return (DockStyle.Top, Rectangle.FromLTRB(r.Left, r.Top - size, r.Right, r.Bottom));
        return (DockStyle.Right, new Rectangle(Math.Max(work.Left, work.Right - r.Width - size), r.Top, r.Width + size, r.Height));
    }

    // The window without the panel: where the video is.
    Rectangle PipOnly(Rectangle b) => !list.Visible ? b : list.Dock switch
    {
        DockStyle.Left => Rectangle.FromLTRB(b.Left + list.Width, b.Top, b.Right, b.Bottom),
        DockStyle.Top => Rectangle.FromLTRB(b.Left, b.Top + list.Height, b.Right, b.Bottom),
        DockStyle.Bottom => b with { Height = b.Height - list.Height },
        _ => b with { Width = b.Width - list.Width },
    };

    // Once: from ShowList, or ahead of it after the PIP's fullscreen (the home page takes ~2 s to load).
    Task InitListAsync() => listInit ??= InitListCoreAsync();
    Task? listInit;

    async Task InitListCoreAsync()
    {
        await list.EnsureCoreWebView2Async(env);
        await list.CoreWebView2!.AddScriptToExecuteOnDocumentCreatedAsync(ListJs);
        list.CoreWebView2.WebMessageReceived += async (_, e) =>
        {
            var msg = e.TryGetWebMessageAsString();
            if (msg == "close") { if (listOpen) ToggleList(); }
            else await PlayFromListAsync(msg);
        };
        // A video opened in the panel some way other than a link click (signed in, 2026-10-05; likely the hover
        // preview, which signed out never shows): back to the list, the panel's player stopped, and into the PIP.
        list.CoreWebView2.SourceChanged += async (_, _) =>
        {
            var url = list.CoreWebView2.Source;
            if (!Program.IsWatchUrl(url)) return;
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

    async Task PlayFromListAsync(string? url)
    {
        var id = Program.VideoId(url);
        if (mode != Mode.Pip || id == null || !System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Za-z0-9_-]+$")) return;
        var list = Program.WatchParam(url, "list") is { } l && System.Text.RegularExpressions.Regex.IsMatch(l, "^[A-Za-z0-9_-]+$") ? l : null;
        int index = int.TryParse(Program.WatchParam(url, "index"), out var i) && i > 0 && list != null ? i : 0;
        var inPage = await EvalAsync(InPageWatchJs.Replace("$ID", JsonSerializer.Serialize(id))
            .Replace("$LIST", JsonSerializer.Serialize(list)).Replace("$INDEX", index.ToString()));
        Program.Log($"from list {id} list={list} index={index}: in-page {inPage}");
        if (inPage?.ValueKind == JsonValueKind.True) return;
        // fallback, if YouTube changes its internals: reload; `retrying` keeps the PIP while it is out of fullscreen
        lastVideo = null;
        retrying = true;
        web.CoreWebView2.Navigate($"https://www.youtube.com/watch?v={id}" + (list != null ? $"&list={list}" + (index > 0 ? $"&index={index}" : "") : ""));
    }

    // Same video and position, in YouTube Music (a plain small window, no fullscreen there) or back in the YouTube PIP.
    async Task ToggleMusicAsync()
    {
        // In Music the track plays on while browsing its other pages (no ?v= then): ask the player. Not on
        // YouTube, whose watch player stays (paused) in the page after going back to the home page.
        var now = await EvalAsync("""
            (p => ({ v: (location.host === 'music.youtube.com' && p?.getVideoData?.()?.video_id) || new URLSearchParams(location.search).get('v'),
                     t: Math.floor(document.querySelector('video')?.currentTime || 0),
                     vol: p?.getVolume ? Math.round(p.getVolume()) : 100, muted: !!p?.isMuted?.() }))(document.querySelector('#movie_player'))
            """);
        string? v = now?.GetProperty("v").GetString();
        int t = now?.GetProperty("t").GetInt32() ?? 0;
        int vol = Math.Clamp(now?.GetProperty("vol").GetInt32() ?? 100, 0, 100);
        bool muted = now?.GetProperty("muted").GetBoolean() ?? false;
        Program.Log($"music toggle from {mode}: v={v} t={t} vol={vol} muted={muted}");
        if (v != null && !System.Text.RegularExpressions.Regex.IsMatch(v, "^[A-Za-z0-9_-]+$")) v = null;
        if (mode == Mode.Music)
        {
            // no track (Music's home): nothing to carry over, its player's volume is not the user's
            if (v == null) { SetMode(Mode.Browse); web.CoreWebView2.Navigate("https://www.youtube.com/"); return; }
            pendingRestore = (0, vol, muted);  // the position goes in the link
            lastVideo = null;
            retrying = true;
            SetMode(Mode.Pip);
            web.CoreWebView2.Navigate($"https://www.youtube.com/watch?v={v}&t={t}s");
        }
        else
        {
            // from any mode: the video being watched, or Music's home when there is none (YouTube home, search)
            SetMode(Mode.Music);
            if (v != null) pendingRestore = (t, vol, muted);
            web.CoreWebView2.Navigate(v != null ? $"https://music.youtube.com/watch?v={v}" : "https://music.youtube.com/");
        }
    }

    async Task<JsonElement?> EvalAsync(string js)
    {
        var args = JsonSerializer.Serialize(new { expression = js, awaitPromise = true, returnByValue = true });
        string result;
        // The page went away under it (on a fresh profile YouTube reloads the first watch page): no value.
        try { result = await web.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate", args); }
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
        if (mode == Mode.Pip) { var r = PipOnly(Bounds); r.Offset(-listShift.X, -listShift.Y); state.PipRect = r; }
        else if (mode == Mode.Browse) state.BrowseRect = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
    }

    void SetMode(Mode next)
    {
        if (next == mode) return;
        RememberRect();
        WindowState = FormWindowState.Normal;
        mode = next;
        list.Visible = false;  // the rects below are without the panel; ShowList adds it back in PIP
        listShift = Point.Empty;
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
        if (next == Mode.Pip) { ShowList(); FitAspect(); }
        SendMenuState();
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
            return new(frame + (list.Visible && beside ? list.Width : 0), frame / 2 + (list.Visible && !beside ? list.Height : 0));
        }
    }

    void FitAspect() => Bounds = Refit(Bounds, Chrome, aspect, Work);

    // The test copy sits off every screen and must stay there: no screen to keep it on.
    Rectangle Work => Program.Test ? new Rectangle(Left, Top, 1 << 20, 1 << 20) : Screen.FromControl(this).WorkingArea;

    // The same video area in the new shape (a 512x288 PIP becomes about 288x512 for a Short), kept at the
    // screen corner it is nearest to and on the screen.
    internal static Rectangle Refit(Rectangle r, Size chrome, double aspect, Rectangle work)
    {
        double area = Math.Max(1, r.Width - chrome.Width) * (double)Math.Max(1, r.Height - chrome.Height);
        var fit = LockAspect(new Rectangle(0, 0, (int)Math.Round(Math.Sqrt(area * aspect)) + chrome.Width, 0), 8, chrome, aspect);
        if (fit.Height > work.Height) fit = LockAspect(new Rectangle(0, 0, 0, work.Height), 6, chrome, aspect);
        int x = r.Left - work.Left <= work.Right - r.Right ? r.Left : r.Right - fit.Width;
        int y = r.Top - work.Top <= work.Bottom - r.Bottom ? r.Top : r.Bottom - fit.Height;
        x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - fit.Width));
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - fit.Height));
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
                var fixedRect = LockAspect(Rectangle.FromLTRB(r.L, r.T, r.R, r.B), (int)m.WParam, Chrome, aspect);
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
                ModifyMenu(menu, IdList, mode == Mode.Pip && web.Source?.Host != "music.youtube.com" ? 0 : MF_GRAYED, IdList, listOpen ? "볼 목록 닫기" : "볼 목록 열기");
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

        // A Short in a PIP at the bottom-right: same area standing up, still in that corner; and back.
        var work = new Rectangle(0, 0, 1920, 1040);
        var pip = new Rectangle(1384, 728, 512, 288);
        var s1 = PipForm.Refit(pip, chrome, tall, work);
        Check(s1.Right == pip.Right && s1.Bottom == pip.Bottom && s1.Width == 295 && s1.Height == 504, $"to tall {s1}");
        var s2 = PipForm.Refit(s1, chrome, wide, work);
        Check(s2.Right == pip.Right && s2.Bottom == pip.Bottom && Math.Abs(s2.Width - pip.Width) <= 2, $"back to wide {s2}");
        // At the top-left it stays there; too tall for the screen it is cut to the screen's height.
        var s3 = PipForm.Refit(new Rectangle(10, 10, 1600, 900), chrome, tall, work);
        Check(s3.X == 10 && s3.Y == 0 && s3.Height == 1040, $"capped {s3}");

        // The watch list goes on the first side with room, the video staying put; with none, right and slid left.
        Check(PipForm.PlaceList(new Rectangle(24, 24, 512, 288), 360, work) == (DockStyle.Right, new Rectangle(24, 24, 872, 288)), "list right");
        Check(PipForm.PlaceList(pip, 360, work) == (DockStyle.Left, new Rectangle(1024, 728, 872, 288)), "list left");
        Check(PipForm.PlaceList(new Rectangle(100, 100, 1700, 300), 360, work) == (DockStyle.Bottom, new Rectangle(100, 100, 1700, 660)), "list below");
        Check(PipForm.PlaceList(new Rectangle(100, 700, 1700, 300), 360, work) == (DockStyle.Top, new Rectangle(100, 340, 1700, 660)), "list above");
        Check(PipForm.PlaceList(new Rectangle(100, 200, 1700, 700), 360, work) == (DockStyle.Right, new Rectangle(0, 200, 2060, 700)), "list slid");

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
