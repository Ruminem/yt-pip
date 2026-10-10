# Changelog

## 0.1.30 — 2026-10-10

- Switching from YouTube to YouTube Music no longer leaves YouTube playing the same song underneath: the side you leave goes silent as soon as the other side plays.
- On a Short in a big PIP with the comments on the left, the window no longer shows a sideways scrollbar.

**한국어**

- YouTube 에서 YouTube Music 으로 전환해도 YouTube 가 같은 곡을 뒤에서 계속 재생하지 않음. 새 쪽이 재생되는 순간 떠나는 쪽이 조용해짐.
- 큰 PIP 에서 Shorts 댓글을 왼쪽에 펼쳐도 창에 가로 스크롤바가 생기지 않음.

## 0.1.29 — 2026-10-10

- YouTube Music now opens in its own view beside YouTube, so switching either way no longer reloads a page. Going back to YouTube shows the video right away and resumes it at Music's position.
- While switching, the side you leave keeps playing until the other side starts, so the sound no longer cuts out.
- On a Short in the PIP, YouTube's left navigation rail no longer covers the top left of the comments.

**한국어**

- YouTube Music 을 YouTube 옆의 별도 화면에 띄움. 어느 쪽으로 전환해도 페이지를 다시 불러오지 않음. YouTube 로 돌아가면 영상이 곧바로 보이고 Music 에서 듣던 시점부터 이어서 재생됨.
- 전환하는 동안 떠나는 쪽이 새 쪽이 재생될 때까지 계속 재생돼서 소리가 끊기지 않음.
- PIP 의 Shorts 에서 YouTube 왼쪽 탐색 막대가 댓글 왼쪽 위를 가리지 않음.

## 0.1.28 — 2026-10-10

- Switching to YouTube Music closes the watch list panel, so coming back to the PIP no longer opens it again.

**한국어**

- YouTube Music 으로 넘어갈 때 '볼 목록' 을 닫음. PIP 로 돌아와도 목록이 다시 펼쳐지지 않음.

## 0.1.27 — 2026-10-10

- Switching to YouTube Music, the track is moved to the carried-over position as soon as Music's player is there, instead of after the whole page has loaded.

**한국어**

- YouTube Music 으로 넘어갈 때 페이지 전체가 다 뜰 때까지 기다리지 않고, Music 플레이어가 준비되는 즉시 넘겨받은 위치로 옮김.

## 0.1.26 — 2026-10-10

- Coming back from YouTube Music, the still picture gives way to the playing video as soon as the page has frames, about 1 s sooner than before, already at the position carried over.
- While the PIP is covered on its way to fullscreen, the page's scrollbar no longer shows at its right edge.

**한국어**

- YouTube Music 에서 돌아올 때 페이지에 영상 프레임이 들어오는 즉시 정지 화면이 재생 중인 영상으로 바뀜. 전보다 1초쯤 빠르고, 넘겨받은 위치에서 바로 이어짐.
- PIP 가 전체화면으로 가는 동안 덮여 있을 때 오른쪽 끝에 페이지 스크롤바가 더는 안 보임.

## 0.1.25 — 2026-10-10

- Going back from YouTube Music to a YouTube video resumes it instead of starting over: at Music's position and volume if Music was playing the same track, else where the video was left.

**한국어**

- YouTube Music 에서 뒤로 가기로 YouTube 영상에 오면 처음부터 시작하지 않고 이어 봄. Music 에서 같은 곡을 듣고 있었으면 Music 의 위치와 볼륨으로, 아니면 영상을 떠날 때의 위치로.

## 0.1.24 — 2026-10-10

- Coming back from YouTube Music to the same video, the PIP shows the frame it left on until the video is fullscreen again (about 3 s), instead of the YouTube page loading in the small window.

**한국어**

- YouTube Music 에서 같은 영상으로 돌아올 때, 영상이 다시 전체화면이 될 때까지(약 3초) 작은 창에 YouTube 페이지가 로딩되는 대신 떠날 때의 화면이 멈춰 보임.

## 0.1.23 — 2026-10-10

- The app no longer hides product cards or the shopping panel. CSS of your own for the pages can go in `%LOCALAPPDATA%\yt-pip\user.css`, read when the app starts; the previous hiding is the snippet to put there if you want it back.
- Going back from YouTube Music to a YouTube video returns the window to the PIP, and to the big window for other pages. Before, it stayed in Music mode. Going forward into Music switches to Music mode.

**한국어**

- 앱이 제품 카드와 쇼핑 패널을 더는 숨기지 않음. 페이지에 넣을 자기 CSS 는 `%LOCALAPPDATA%\yt-pip\user.css` 에 두면 앱을 켤 때 읽음. 예전처럼 숨기려면 그 선택자를 이 파일에 넣으면 됨.
- YouTube Music 에서 뒤로 가기로 YouTube 영상에 오면 창이 PIP 로, 다른 페이지면 큰 창으로 돌아감. 전에는 Music 모드로 남았음. 앞으로 가기로 Music 에 들어가면 Music 모드로 바뀜.

## 0.1.22 — 2026-10-10

- Picking in the list the playlist the PIP already plays from shows that playlist and leaves the video playing. Before, a click after hovering landed on YouTube's preview and started the playlist's first video.
- While a new video gets to the PIP it shows at once: its thumbnail, then the video itself, instead of a blank window or the bare watch page. This also covers the way from a Short to another video.
- The watch page's top bar and player controls no longer flash over the PIP for about 0.2 s as it goes fullscreen.

**한국어**

- 목록에서 PIP 가 이미 재생 중인 재생목록을 고르면 그 재생목록만 보여 주고 영상은 그대로 둠. 전에는 마우스를 올린 뒤 누르면 클릭이 YouTube 미리보기에 떨어져 재생목록 첫 영상이 틀어졌음.
- 새 영상이 PIP 로 들어오는 동안 빈 창이나 영상 페이지 대신 썸네일, 이어서 영상 자체를 바로 보여 줌. Short 에서 다른 영상으로 갈 때도 마찬가지임.
- 전체화면으로 바뀔 때 영상 페이지의 상단 바와 플레이어 컨트롤이 약 0.2초 PIP 위에 비치던 것이 없어짐.

## 0.1.21 — 2026-10-10

- The Shorts button back to the big window is now a back arrow named 돌아가기, and sits beside YouTube's buttons at the top right instead of over them.
- A video picked in the list after a Short fills the PIP about 0.8 s sooner: it no longer waits for the watch page to take the new video before going fullscreen.

**한국어**

- Shorts 에서 큰 창으로 돌아가는 버튼이 '돌아가기'라는 이름의 뒤로 가기 화살표로 바뀜. 오른쪽 위 YouTube 버튼 위에 겹치지 않고 그 옆에 놓임.
- Shorts 다음에 목록에서 고른 영상이 약 0.8초 빨리 PIP 를 채움. 영상 페이지가 새 영상을 받을 때까지 기다리지 않고 전체화면으로 감.

## 0.1.20 — 2026-10-10

- The PIP keeps its top-left corner when it changes shape between a Short and another video, moving only to stay on the screen. Before, a PIP in the lower half of the screen grew upward from its bottom edge.
- Opening a playlist from YouTube's ☰ menu in the list shows that playlist. Signed in, YouTube resumed it on one of its videos instead, which then played in the PIP.
- A Short reached from another video starts with its comments closed. YouTube reopened the comments last left open; the next Short by the wheel still keeps them.
- The log records what a panel opened over the fullscreen video holds, to find why the title opened an empty one.

**한국어**

- Shorts 와 일반 영상 사이에서 PIP 모양이 바뀔 때 왼쪽 위 모서리를 그대로 둠. 화면을 벗어날 때만 옮김. 전에는 화면 아래쪽 반에 있는 PIP 가 아래 변을 기준으로 위로 늘어났음.
- 목록에서 YouTube ☰ 메뉴의 재생목록을 열면 그 재생목록이 보임. 로그인 상태에서는 YouTube 가 재생목록의 한 영상으로 이어 보기를 열어 그 영상이 PIP 에서 재생됐음.
- 다른 영상에서 넘어온 Short 는 댓글창이 닫힌 채로 시작함. YouTube 가 마지막에 열어 둔 댓글창을 다시 열었음. 휠로 넘긴 다음 Short 는 지금처럼 댓글창을 유지함.
- 전체화면 영상 위에 열린 패널의 구성을 로그에 남김. 제목을 누르면 빈 패널이 열리는 원인을 찾기 위한 것임.

## 0.1.19 — 2026-10-10

- A Short's comments beside the PIP stay when the window is resized smaller, instead of disappearing.
- Fullscreen over the whole monitor tells the taskbar it is fullscreen, so the taskbar goes behind the video while the window is active; Windows did not always notice by itself.

**한국어**

- PIP 옆 Shorts 댓글창이 창을 줄여도 사라지지 않음.
- 모니터 전체 화면이 작업 표시줄에 전체화면임을 알려서, 창이 활성인 동안 작업 표시줄이 영상 뒤로 내려감. Windows 가 스스로 알아채지 못할 때가 있었음.

## 0.1.18 — 2026-10-10

- Always on top holds after the app is started in the background (by a script, say): Windows didn't let it go on top then, so the window sat below others with the menu still checked until the option was turned off and on. It now goes on top the first time the window is clicked.

**한국어**

- 앱을 백그라운드에서 띄웠을 때(스크립트 등)도 항상 위가 유지됨. 그때는 Windows 가 맨 위로 올려 주지 않아서 메뉴에 체크가 돼 있어도 창이 다른 창 아래에 있었고, 옵션을 껐다 켜야 했음. 이제 창을 처음 클릭할 때 맨 위로 올라감.

## 0.1.17 — 2026-10-10

- A Short's comments beside the PIP have a glass background: the video's colours, blurred, show through the panel as they do through YouTube's buttons over the video, and follow the Short as the wheel moves on.

**한국어**

- PIP 옆에 열리는 Shorts 댓글창에 글래스 배경이 생김. 영상 위 YouTube 버튼처럼 영상 색이 흐리게 비쳐 보이고, 휠로 다음 Shorts 로 넘기면 그 영상의 색을 따라감.

## 0.1.16 — 2026-10-10

- YouTube's own fullscreen on a Short (a double-click, say) no longer gets stuck: from the big window it goes to the Shorts PIP as other videos do, and the big window button no longer brings back a fullscreen page without YouTube's top bar.

**한국어**

- Shorts 에서 YouTube 자체 전체화면(더블클릭 등)이 켜져도 더는 꼬이지 않음. 큰 창에서였으면 다른 영상처럼 Shorts PIP 가 되고, 큰 창 버튼을 눌렀을 때 YouTube 위쪽 막대가 없는 전체화면 페이지로 돌아오던 것이 없어짐.

## 0.1.15 — 2026-10-10

- A Short in the PIP has a button left of YouTube's ⋮ that goes back to the big window, as leaving fullscreen does for other videos (the Shorts page has no fullscreen). It shows with YouTube's own top controls when the mouse is over the window.

**한국어**

- PIP 의 Shorts 에 YouTube ⋮ 왼쪽으로 큰 창으로 돌아가는 버튼이 생김. 다른 영상에서 전체화면을 나가는 것과 같음(Shorts 페이지에는 전체화면이 없음). 마우스가 창 위에 있을 때 YouTube 위쪽 컨트롤과 같이 보임.

## 0.1.14 — 2026-10-09

- Shorts play in a 9:16 PIP on YouTube's own Shorts page: the mouse wheel goes to the next Short, and each Short loops instead of autoplay moving on.
- A Short's comments open beside the PIP, which widens for them on whichever side has room as the watch list does, so the video keeps its size; the comments button closes them again, and the PIP goes back to where it was.
- The Shorts buttons' icons sit on their circles again.

**한국어**

- Shorts 를 YouTube 의 Shorts 페이지 그대로 9:16 PIP 에서 재생함. 마우스 휠로 다음 Shorts 로 넘어가고, 자동재생으로 넘어가지 않고 한 편을 반복함.
- Shorts 댓글이 PIP 옆에 열림. 볼 목록처럼 공간이 남은 쪽으로 창이 넓어져 영상 크기는 그대로이고, 댓글 버튼을 다시 누르면 닫히며 PIP 가 원래 자리로 돌아옴.
- Shorts 버튼의 아이콘이 다시 동그라미 가운데에 놓임.

## 0.1.13 — 2026-10-06

- The watch list opens on whichever side of the PIP the screen has room for: right, else left, below or above, so the PIP stays where it is. Only with no room anywhere does it go right and push the PIP left as before, and closing the list now moves the PIP back to where it was.

**한국어**

- 볼 목록이 PIP 의 오른쪽, 안 되면 왼쪽·아래·위 중 화면에 공간이 남은 쪽에 열려 PIP 가 제자리에 있음. 어디에도 공간이 없을 때만 전처럼 오른쪽에 열며 PIP 를 왼쪽으로 밀고, 이제 목록을 닫으면 PIP 가 원래 자리로 돌아옴.

## 0.1.12 — 2026-10-05

- No change in the app: the release is uploaded to Google Drive with the project's own Google Cloud client instead of rclone's shared one, which is being shut down. The download link stays the same.

**한국어**

- 앱은 바뀐 것 없음. 드라이브 업로드를 곧 끊기는 rclone 공용 client 대신 직접 만든 구글 클라우드 client 로 함. 다운로드 링크는 그대로임.

## 0.1.11 — 2026-10-05

- The watch list shows where the PIP is in the video it plays: the red bar under its thumbnail follows playback and seeks right away, instead of staying where it was when the list loaded.
- The video the PIP plays is highlighted in the watch list.

**한국어**

- 볼 목록이 PIP 에서 보는 영상의 위치를 보여 줌. 썸네일 아래 빨간 막대가 목록을 불러올 때 그대로 남지 않고 재생과 시점 이동을 바로 따라감.
- 볼 목록에서 PIP 가 지금 틀고 있는 영상이 강조됨.

## 0.1.10 — 2026-10-05

- Letting go of the time bar in the PIP is no longer missed. YouTube pauses the video while the bar is held, and the PIP used to bring its title bar back for that pause, changing the window under the held button; the frame now stays as it is until the bar is let go.

**한국어**

- PIP 에서 시간축을 놓은 것을 더는 놓치지 않음. 시간축을 잡고 있는 동안 유튜브가 영상을 멈추는데, 그때마다 PIP 가 제목 표시줄을 다시 꺼내 누른 채인 버튼 밑에서 창이 바뀌었음. 이제 시간축을 놓을 때까지 창 틀이 그대로임.

## 0.1.9 — 2026-10-05

- Letting go of the time bar in the PIP seeks there at once. Before, pulling up a little while dragging opened YouTube's fine scrubbing, which stayed after letting go and waited for another click.
- A video opened in the watch list some other way than its link (signed in, likely the preview that plays on a thumbnail) plays in the PIP too, instead of in the list; the list goes back to where it was.

**한국어**

- PIP 에서 시간축을 놓으면 바로 그 자리로 이동함. 전에는 끌다가 조금 위로 당기면 유튜브의 세밀 탐색이 켜져, 손을 떼도 남아서 한 번 더 클릭해야 했음.
- 볼 목록에서 링크가 아닌 길로 연 영상(로그인 상태, 썸네일 위에서 도는 미리보기로 보임)도 목록 안이 아니라 PIP 에서 틀어지고, 목록은 보던 자리로 돌아감.

## 0.1.8 — 2026-10-05

- No change in the app: this is the first release built and uploaded by GitHub Actions.

**한국어**

- 앱은 바뀐 것 없음. GitHub Actions 가 빌드하고 올린 첫 릴리스임.

## 0.1.7 — 2026-10-05

- Going back from the PIP and then forward to the same video turns it into the PIP again, instead of leaving it on the watch page in the big window.

**한국어**

- PIP 에서 뒤로 갔다가 앞으로 가서 같은 영상으로 돌아오면, 큰 창의 시청 페이지에 머물지 않고 다시 PIP 가 됨.

## 0.1.6 — 2026-10-05

- Picking a video turns the window into the PIP at once, dark until the video is fullscreen in it, instead of showing the watch page in the big window first. The video shows after about 1.5 s instead of 2.4 s.

**한국어**

- 영상을 고르면 큰 창에 시청 페이지를 먼저 보여 주지 않고, 바로 PIP 창이 되어 영상이 전체화면이 될 때까지 어둡게 가려 둠. 영상이 보이기까지 2.4초쯤 걸리던 것이 1.5초쯤으로 줄었음

## 0.1.5 — 2026-10-05

- A video picked inside a playlist in the watch list plays on through that playlist in the PIP, and the list shows the playlist (Mixes have no playlist page, so the list stays where it was for those).
- A back button beside the watch list's close button, shown when there is somewhere to go back to: playlist → search results → home.

**한국어**

- 볼 목록에서 재생목록 안의 영상을 고르면 PIP 가 그 재생목록을 이어서 재생하고, 볼 목록에 그 재생목록이 뜸 (믹스는 재생목록 페이지가 없어서 볼 목록이 그대로 있음)
- 볼 목록 닫기 버튼 옆에 뒤로 버튼을 넣었음. 돌아갈 곳이 있을 때만 보이고, 재생목록 → 검색 결과 → 홈 순서로 돌아감

## 0.1.4 — 2026-10-05

- The PIP takes the video's own shape: Shorts play in an upright window instead of a 16:9 one, at the same size and in the same screen corner.
- Dragging the PIP no longer turns on 2x speed; holding the video down without moving still does.
- Captions start off on every video, even after they were turned on for an earlier one.
- In the watch list, the arrow left of the search box now goes back from the search results instead of only closing the box.

**한국어**

- PIP 가 영상 모양을 따라감: 쇼츠는 16:9 창 대신 세로 창으로 재생되고, 크기와 화면 모서리 위치는 그대로임
- PIP 를 드래그해도 2배속이 켜지지 않음. 움직이지 않고 길게 누르면 그대로 2배속이 됨
- 앞 영상에서 자막을 켰더라도 영상마다 자막이 꺼진 채로 시작함
- 볼 목록에서 검색창 왼쪽 화살표를 누르면 검색창만 닫히던 것을, 검색 결과에서 이전 페이지로 돌아가게 고쳤음

## 0.1.3 — 2026-10-05

- The very first video on a new install now goes into the PIP. YouTube reloads that first page, which used to stop the app at an error dialog.
- An unexpected error is written to the log instead of stopping the app at a dialog.

**한국어**

- 처음 설치한 뒤 첫 영상도 PIP 로 들어감. YouTube 가 그 첫 페이지를 한 번 다시 불러오는데, 그때 앱이 에러 창에서 멈추던 것을 고쳤음
- 예상 못 한 오류는 에러 창으로 앱을 멈추지 않고 로그에 남김

## 0.1.2 — 2026-10-05

- The watch list loads in the background once the PIP is up, so its first open takes about 0.7 s instead of 2.4 s.

**한국어**

- PIP 가 뜨면 볼 목록을 미리 불러 둬서 처음 열 때 2.4초 걸리던 것이 0.7초쯤으로 줄었음

## 0.1.1 — 2026-10-05

- Three buttons in the player's own controls, left of subtitles and settings: watch list (in the PIP), YouTube Music switch, and always on top.
- Opening or closing the watch list no longer makes the video jump sideways for a moment.

**한국어**

- 플레이어 컨트롤 바의 자막·설정 왼쪽에 버튼 셋을 넣었음: 볼 목록(PIP 에서), YouTube Music 전환, 항상 위에 표시
- 볼 목록을 열고 닫을 때 영상이 잠깐 옆으로 튀었다 돌아오던 것을 고쳤음

## 0.1.0 — 2026-10-05

- First release: one `yt-pip.exe`, no install and no .NET needed (WebView2 comes with Windows 11).
- Picking a video turns the window into a small PIP using YouTube's own fullscreen player; the title bar hides while it plays and comes back when paused.
- The PIP keeps 16:9 while resizing, and dragging the video moves the window.
- Title bar menu (the icon at the top left, or Alt+Space): always on top, fullscreen over the whole monitor, YouTube Music switch, and the watch list.
- Watch list: YouTube's home beside the PIP, so the next video starts without leaving the PIP.
- YouTube ↔ YouTube Music switch keeps the track, position and volume.
- Product cards over videos, the Shorts "View products" button and its panel are hidden.
- The playlist panel opened from fullscreen shows its videos.

**한국어**

- 첫 배포: `yt-pip.exe` 한 파일, 설치나 .NET 이 필요 없음 (WebView2 는 Windows 11 에 들어 있음)
- 영상을 고르면 YouTube 자체 전체화면 플레이어로 작은 PIP 창이 됨. 재생 중에는 제목표시줄이 숨고 일시정지하면 돌아옴
- PIP 는 크기를 바꿔도 16:9 를 유지하고, 영상을 끌면 창이 움직임
- 제목표시줄 메뉴(왼쪽 위 아이콘 또는 Alt+Space): 항상 위에 표시, 모니터 전체 화면, YouTube Music 전환, 볼 목록
- 볼 목록: PIP 옆에 YouTube 홈이 붙어 PIP 를 벗어나지 않고 다음 영상을 틀 수 있음
- YouTube ↔ YouTube Music 전환 때 곡·재생 위치·볼륨이 이어짐
- 영상 위 제품 카드, 쇼츠의 "제품 보기" 버튼과 그 패널이 안 보임
- 전체화면에서 연 재생목록 패널에 영상 목록이 제대로 보임
