# yt-pip

**English** · [한국어](#korean)

A small Windows window for watching YouTube on top of your work. Pick a video and the
window turns into a picture-in-picture player that keeps the video's shape; pick
another from the watch list beside it without leaving the PIP.

It is YouTube's own website in a WebView2 window, with the page laid out for a small
window. Nothing is downloaded, and ads and everything else YouTube shows are left as
they are. CSS of your own for the pages can go in `%LOCALAPPDATA%\yt-pip\user.css`,
read when the app starts.

Not affiliated with, endorsed by or sponsored by YouTube or Google.

## What it does

- **PIP from any video.** Picking a video turns the window into a PIP using YouTube's
  own fullscreen player. The title bar hides while it plays and comes back when paused,
  resizing keeps the video's shape, and dragging the video moves the window.
- **Shorts** play in an upright PIP on YouTube's own Shorts page: the mouse wheel goes
  to the next Short, and comments open beside the PIP.
- **Watch list.** YouTube's home beside the PIP, on whichever side the screen has room
  for, so the next video starts without leaving the PIP.
- **YouTube ↔ YouTube Music** switch that keeps the track, position and volume.
- **Always on top**, and fullscreen over the whole monitor.
- **Back to the big window**: leave YouTube's fullscreen (the button or `Esc`), or the
  button left of `⋮` on a Short.

The buttons sit in the player's own controls (watch list, YouTube Music, always on
top), and the same items are in the title bar menu (the icon at the top left, or
`Alt+Space`).

## Install

Download `yt-pip.exe` from [Releases](https://github.com/Ruminem/yt-pip/releases)
and run it. It is one self-contained file (about 50 MB): no installer and no .NET
needed. It needs the WebView2 runtime, which comes with Windows 11.

You can sign in to YouTube in the window as in a browser. The sign-in, the window
positions and a log live in `%LOCALAPPDATA%\yt-pip`; delete that folder to start over.

## Building it yourself

Needs the .NET 10 SDK.

```sh
dotnet build -c Release
bin\Release\net10.0-windows\yt-pip.exe --selftest
```

The release exe is the same project published as one file:

```sh
dotnet publish -c Release -r win-x64 -p:SelfContained=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o out
```

Everything is in `Program.cs`: the window, and the scripts and styles it puts into
YouTube's page.

## Status

Windows only. Developed and checked on Windows 11. YouTube changes its page from time
to time, and the parts that lay it out for the PIP can break when it does — please
open an issue.

## License

[Apache-2.0](LICENSE). Third-party notices are in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

## Korean

[English](#yt-pip) · **한국어**

작업하면서 YouTube 를 띄워 두고 보는 작은 윈도우 창임. 영상을 고르면 창이 영상 모양을
그대로 따르는 PIP(화면 속 화면) 플레이어가 되고, 옆의 볼 목록에서 다음 영상을 골라도
PIP 를 벗어나지 않음.

WebView2 창에 YouTube 웹사이트를 그대로 띄우고 작은 창에 맞게 배치만 바꿈. 아무것도
내려받지 않고 광고를 비롯해 YouTube 가 보여 주는 것은 건드리지 않음. 페이지에 넣을 자기
CSS 는 `%LOCALAPPDATA%\yt-pip\user.css` 에 두면 앱을 켤 때 읽음.

YouTube·Google 과 관계없고, 그쪽이 보증하거나 후원하는 것이 아님.

### 하는 일

- **어느 영상이든 PIP.** 영상을 고르면 YouTube 자체 전체화면 플레이어로 PIP 창이 됨.
  재생 중에는 제목 표시줄이 숨고 일시정지하면 돌아오며, 크기를 바꿔도 영상 모양이
  유지되고, 영상을 끌면 창이 움직임.
- **Shorts** 는 YouTube 의 Shorts 페이지 그대로 세로 PIP 에서 재생됨. 마우스 휠로 다음
  Shorts 로 넘어가고, 댓글은 PIP 옆에 열림.
- **볼 목록.** PIP 옆, 화면에 공간이 남은 쪽에 YouTube 홈이 붙어 PIP 를 벗어나지 않고
  다음 영상을 틀 수 있음.
- **YouTube ↔ YouTube Music** 전환. 곡·재생 위치·볼륨이 이어짐.
- **항상 위에 표시**, 그리고 모니터 전체 화면.
- **큰 창으로 돌아가기**: YouTube 전체화면을 나가거나(버튼 또는 `Esc`), Shorts 에서는
  `⋮` 왼쪽 버튼.

버튼은 플레이어 자체 컨트롤 바에 있고(볼 목록, YouTube Music, 항상 위), 같은 항목이 제목
표시줄 메뉴(왼쪽 위 아이콘 또는 `Alt+Space`)에도 있음.

### 설치

[Releases](https://github.com/Ruminem/yt-pip/releases)에서 `yt-pip.exe` 를 받아 실행.
파일 하나로 다 들어 있어서(약 50 MB) 설치도 .NET 도 필요 없음. WebView2 런타임이 있어야
하는데 Windows 11 에는 들어 있음.

창 안에서 브라우저처럼 YouTube 에 로그인할 수 있음. 로그인 정보, 창 위치, 로그는
`%LOCALAPPDATA%\yt-pip` 에 있고, 그 폴더를 지우면 처음 상태로 돌아감.

### 직접 빌드하기

.NET 10 SDK 가 필요함.

```sh
dotnet build -c Release
bin\Release\net10.0-windows\yt-pip.exe --selftest
```

릴리스 exe 는 같은 프로젝트를 파일 하나로 publish 한 것임:

```sh
dotnet publish -c Release -r win-x64 -p:SelfContained=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o out
```

전부 `Program.cs` 에 있음: 창, 그리고 YouTube 페이지에 넣는 스크립트와 스타일.

### 상태

Windows 전용. Windows 11 에서 개발하고 확인했음. YouTube 가 페이지를 이따금 바꾸는데,
그러면 PIP 에 맞게 배치하는 부분이 깨질 수 있음 — 이슈를 열어 주면 고맙겠음.

### 라이선스

[Apache-2.0](LICENSE). 서드파티 고지는 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)에 있음.
