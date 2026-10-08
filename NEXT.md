- 여기까지 됨 (2026-10-09 — 0.1.14 로컬 릴리스): GitHub Actions 가 이달 무료 제공량 소진(추정, 사용액 $21.17 = 제공 할인액)으로 잡을 안 띄워서 release.yml 과 같은 단계를 PC 에서 함 — 같은 publish 옵션으로 빌드·selftest, PC rclone(v1.75.1, 원격 gdrive)으로 기존 exe 덮어쓰고 yt-pip-0.1.14.exe 로 이름 바꿈(드라이브 ID 그대로), gh release create v0.1.14(대상 5c60a17, notes 는 CHANGELOG 항목). 11월 1일 제공량이 다시 차기 전 릴리스도 이렇게 함
- 그 전 (같은 날): Shorts 를 YouTube Shorts 페이지 그대로 9:16 PIP 에서 재생(60bc4fd, 휠로 다음·반복 재생), 댓글은 PIP 옆으로 창을 넓혀 띄움(0e0371c). 댓글을 열 때 좁은 PIP 위에 패널이 겹쳐 깜빡이던 것은 페이지가 배치를 마친 뒤(panel:placed)에 창을 넓히고 그동안 영상 폭(--yt-pip-vw)을 고정·패널은 넓어진 뒤에만 보이게 고침(6977aed, 화면 녹화 cast.mjs 로 전후 비교). 닫을 때는 창이 줄기 전 약 50ms 오른쪽이 빈 검은 띠로 남음 — 없애려면 닫는 순서를 바꿔야 함
- 다음 할 것: 0.1.14 설치본으로 실기기 확인 (직접, 설치본은 교체함). 확인할 것: 진짜 휠 한 칸, 영상 드래그, 아이콘, 오른쪽 공간 없을 때 댓글 왼쪽, 그리고 아래 셋
- 아이디어 (사용자 메모): Shorts 댓글창 배경을 댓글 여는 버튼처럼 글래스(반투명·블러)로. 버튼은 영상 위라 비치지만 댓글창은 영상 옆이라 뒤가 검은 배경뿐 — 흐리게 늘린 영상(캔버스나 video 사본)을 패널 뒤에 깔아야 효과가 날 것으로 추정, 안 재 봄
- 막힌 것: 없음 (Actions 는 11월 1일까지 못 씀 — 위 로컬 절차로 우회)

- 여기까지 됨 (2026-10-06 — 0.1.13, 개발 빌드로 사용자 확인·설치본 교체): 볼 목록을 PIP 의 오른쪽 → 왼쪽 → 아래 → 위 중 화면(작업 영역)에 패널이 들어가는 첫 쪽에 붙여 영상은 제자리(PlaceList, 위·아래는 높이 = 옆일 때 폭과 같은 값). 어디에도 안 들어가면 전처럼 오른쪽에 붙이고 창을 밀며, 민 거리를 listShift 에 두었다가 닫을 때 되돌림. 패널이 오른쪽이라고 가정하던 곳(PipRect 기억·비율 맞출 때의 Chrome·닫을 때 줄이기)은 PipOnly 로 방향을 따짐. selftest 에 다섯 경우 — 시험 사본은 화면 밖이라 늘 오른쪽이므로 다른 쪽은 selftest 와 개발 빌드로만 봄. rclone 해시 검사도 yt-pip CI 에서 처음 돌아 통과
- 다음 할 것: 실기기 확인 셋(진짜 Esc 키, Music → YouTube, 볼 목록에서 마우스 뒤로 단추) (직접). 드라이브 범위를 drive.file 로 좁힐지는 아래 그대로 미정
- 막힌 것: 없음

- 여기까지 됨 (2026-10-05 — 0.1.12, 설치본 교체·사용자 확인): 앱 변경 없이 0.1.12 를 내어 자체 client 업로드를 확인(공용 client id 경고 0줄, 드라이브 ID 그대로). release.yml 이 받은 rclone zip 의 SHA256 을 rclone 사이트·GitHub 릴리스 SHA256SUMS 값과 맞춰 보고 다르면 멈춤(4b103e0, playroom 2ac658a) — rclone 버전을 올리면 URL 과 해시를 같이 바꿈. 두 저장소 모두 private, 비밀값은 release.yml 만 씀
- 다음 할 것: 해시 검사는 같은 줄인 playroom 0.2.8 CI 에서 통과함(yt-pip 은 다음 릴리스에서 처음 돎). scope drive 는 드라이브 전체 권한이라, 좁히려면 drive.file 로 새 client 가 exe 를 새로 올려야 하고 링크가 한 번 바뀜 — 정하지 않았음 (직접). 실기기 확인 셋은 아래 그대로. 토큰은 반년 안 쓰면 끊김 — 그때 PC 에서 rclone config reconnect gdrive: 뒤 TOKEN 비밀값만 다시 넣기
- 막힌 것: 없음

- 여기까지 됨 (2026-10-05 — rclone 자체 client id): 구글 클라우드에 rclone-ci OAuth client(데스크톱, 앱 게시됨·미인증 — 혼자 쓰니 인증 안 받음)를 만들고 PC rclone 원격 gdrive 를 scope drive 로 다시 인증. yt-pip·playroom 두 저장소에 비밀값 RCLONE_CONFIG_GDRIVE_TOKEN·_CLIENT_ID·_CLIENT_SECRET 을 넣고 release.yml 이 셋과 scope drive 를 씀(23ea068, playroom c73f171). drive 범위라 옛 drive.file 이 만든 파일이 그대로 보여 ID·링크 안 바뀜 — CI 와 같은 환경 변수만으로 Apps/yt-pip·Apps/playroom 을 읽어 확인
- 다음 할 것: playroom 다음 릴리스에서 드라이브 ID 가 그대로인지 보기. yt-pip 은 0.1.12(앱 변경 없음)로 확인됨 — 공용 client id 경고 0줄(0.1.11 은 5줄), ID 1pga0jvr3fT-ZqUXyEZnCCWKV1RitmIiE 그대로. 실기기 확인 셋(Esc 키, Music → YouTube, 목록에서 마우스 뒤로)은 아래 그대로
- 막힌 것: 없음

- 여기까지 됨 (2026-10-05 — v0.1.8–0.1.11, 사용자가 설치본에서 매번 확인): 릴리스를 release.ps1 대신 GitHub Actions(.github/workflows/release.yml)가 함 — csproj 버전을 올려 푸시하면 빌드·selftest·드라이브 덮어쓰기(같은 ID, 이름은 yt-pip-<버전>.exe)·GitHub 릴리스까지 1분 40초쯤. 0.1.9: 시간축을 위로 당겨 켜진 유튜브 세밀 탐색이 손을 떼도 남던 것을 놓는 순간 Esc 로 끝냄. 볼 목록에서 링크 클릭이 아닌 길로 연 영상(로그인 상태의 썸네일 미리보기로 보임)을 호스트의 SourceChanged 에서 잡아 PIP 로 보내고 패널은 stopVideo 뒤 뒤로 감. 0.1.10: 시간축을 잡는 동안 유튜브가 영상을 멈추면 제목 표시줄이 다시 나와 창 틀이 바뀌었음(안쪽 높이 280→249) — 그동안 일시정지를 호스트에 안 알림. 실제 마우스로는 손 뗀 것을 놓쳤고 CDP 입력으로는 재현이 안 됐음. 0.1.11: PIP 재생 위치를 1% 단위로 패널에 보내 썸네일 빨간 막대를 다시 그리고, 지금 재생 중인 항목에 배경·빨간 테두리를 둘렀음
- 다음 할 것: rclone 자체 client_id 만들기. rclone 공용 client_id 가 2026년 안에 끊긴다고 매 릴리스 로그에 경고가 뜸 — 구글 클라우드에서 client_id 를 만들고 다시 인증한 뒤 RCLONE_CONFIG_GDRIVE_TOKEN 비밀값을 yt-pip·playroom 둘 다 바꿔야 함 (직접). 그 밖에 아직 실기기에서 안 본 것: 진짜 Esc 키, Music → YouTube 돌아오기, 볼 목록에서 마우스 뒤로 단추
- 막힌 것: 없음. 시험 사본은 로그인이 안 돼 썸네일 미리보기와 유튜브 원래 진행 막대가 안 뜨고, CDP 입력은 OS 를 안 거쳐 창 틀·마우스 캡처 문제는 재현 안 됨 — 그런 건 개발 빌드(bin\Release\net10.0-windows\yt-pip.exe)를 사용자가 써 보게 하는 게 빠름. Claude 앱(MSIX) 안에서는 %LOCALAPPDATA%\yt-pip 가 가상화된 옛 사본으로 보여 사용자 앱의 실제 log.txt 를 못 읽음
