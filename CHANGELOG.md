# Changelog

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
