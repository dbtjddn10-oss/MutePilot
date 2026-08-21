# MutePilot 아이콘 자산 위치

최종 배포용 아이콘이 준비되면 이 폴더에 아래 이름으로 둡니다.

* `app-icon.ico`: 창, taskbar, 실행 파일용 다중 해상도 Windows 아이콘
* `brand-icon.png`: About 화면이나 문서에서 사용할 투명 배경 브랜드 이미지

현재 저장소에는 확정된 바이너리 아이콘 원본이 없어서 기존 placeholder 동작을 유지합니다. 임시 이미지를 최종 자산처럼 포함하지 않습니다.

프로젝트는 파일이 실제로 존재할 때만 `app-icon.ico`를 executable resource와 application icon으로 포함합니다. 실행 중에도 같은 resource를 MainWindow와 tray에서 우선 사용하며, 없거나 읽지 못하면 현재 Windows 기본 아이콘으로 안전하게 돌아갑니다. `brand-icon.png`가 준비되면 About card에서 자동으로 표시합니다.
