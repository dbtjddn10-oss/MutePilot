# MutePilot 아이콘 자산 위치

최종 배포용 자산은 아래 이름으로 관리합니다.

* `app-icon.ico`: 창, taskbar, 실행 파일용 다중 해상도 Windows 아이콘
* `brand-icon.png`: Sidebar, About, mini-HUD에서 사용할 투명 배경 브랜드 이미지
* `toss-support-qr.jpg`: Toss에서 직접 생성한 개발 후원 QR 원본

현재 세 최종 자산을 실제 앱에 연결했습니다. 원본 그림과 QR encoded data는 앱에서 수정하거나 다시 생성하지 않습니다.

프로젝트는 파일이 실제로 존재할 때만 각 자산을 WPF resource로 포함합니다. `app-icon.ico`를 읽지 못하면 Windows 기본 아이콘을 사용하고, Toss JPG를 읽지 못하면 로컬 계좌정보 QR을 생성합니다.
