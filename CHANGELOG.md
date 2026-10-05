# Changelog

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
