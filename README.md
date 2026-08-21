# MutePilot

## MutePilot 소개

MutePilot은 사용자가 지정한 전역 단축키로 Windows의 마스터 오디오와 개별 애플리케이션 오디오 세션을 음소거하거나 다시 켤 수 있도록 만드는 가벼운 Windows 유틸리티입니다.

## 만들게 된 이유

게임이나 작업 중에 여러 창을 오가지 않고 필요한 소리만 빠르게 제어하는 도구를 직접 만들어 보기 위해 시작했습니다. Windows 오디오 API와 전역 단축키가 실제 데스크톱 프로그램에서 어떻게 연결되는지 배우는 과정도 함께 기록합니다.

## 현재 개발 상태

현재는 **v0.8 테마 UI와 프리셋 입력 개발 단계**입니다.

메인 화면을 네이비·차콜 기반의 사이드바 대시보드로 다시 구성했습니다. 탐색은 같은 `MainWindow` 안의 `홈 / 대시보드`와 `실행 설정` 두 화면으로 단순화했습니다. 앱을 열면 Master Audio와 Application Audio가 함께 있는 대시보드가 먼저 보이며, 오버레이·Windows 시작·실행 권한·About은 실행 설정 화면에 모았습니다. 기본 창 크기는 1100×740 WPF DIP이고 현재 화면 작업 영역이 작으면 그 안에 맞춰 열립니다.

화면 테마는 `기본 (다크)`, `화이트`, `핑크` 세 가지입니다. Sidebar 아래의 selector를 기본 Windows ComboBox처럼 보이지 않는 둥근 theme-aware control로 다듬었습니다. 색상은 WPF `ResourceDictionary`로 분리했으며 선택 즉시 화면에 반영되고 `%LocalAppData%\MutePilot\settings.json`에 저장됩니다. 기존 설정 파일에 `theme`이 없으면 다크모드로 시작하며 파일을 자동으로 다시 쓰지 않습니다.

Application Audio에는 현재 audio session의 process에서 실행 파일 경로를 확인할 수 있을 때 Windows 연결 아이콘을 표시합니다. 같은 실행 파일과 앱은 메모리 cache를 사용해 목록 갱신마다 아이콘을 다시 추출하지 않습니다. 경로 접근이 거부되거나 앱이 실행 중이 아니면 안전한 공통 아이콘을 사용합니다. 원하는 앱이 목록에 없을 때는 앱에서 소리를 재생한 뒤 `새로고침`을 누르라는 안내도 화면에 표시합니다.

Master와 각 앱의 볼륨 프리셋은 slider뿐 아니라 0~100 정수 입력으로도 바꿀 수 있습니다. 숫자를 입력하면 slider와 전환 버튼이 함께 갱신되고, slider를 움직이면 숫자 칸도 같은 값으로 바뀝니다. 공백은 제거해 처리하며 빈 값, 소수, 음수, 100보다 큰 값은 기존 설정을 유지한 채 한국어 오류를 표시합니다. Enter와 기존 전환 버튼은 같은 검증 경로를 사용합니다.

최종 `app-icon.ico`와 `brand-icon.png`를 `src/MutePilot/Assets/`에 적용했습니다. 앱 아이콘은 executable·MainWindow·Windows taskbar·tray에 사용하고, 브랜드 이미지는 Sidebar·About·mini-HUD에 표시합니다. 리소스를 실제로 읽지 못한 경우에만 Windows 기본 아이콘으로 돌아갑니다.

About에는 일반 저자 표기 `Made by 유성우`와 별도의 `후원하기` 버튼이 있습니다. 후원 창은 Toss에서 생성한 `toss-support-qr.jpg`를 크게 표시하고, `계좌번호 복사` 뒤에는 창 안에서 성공 여부를 알려 줍니다. Toss QR 원본이 없거나 읽히지 않을 때만 국민은행 계좌정보를 담은 로컬 QR로 돌아갑니다. 실제 휴대폰에서 표시되는 Toss 화면은 이번 작업에서 확인하지 않았습니다.

.NET 8 WPF 앱에서 Windows 기본 출력 장치의 마스터 음소거 상태를 읽고, 버튼으로 음소거와 음소거 해제를 전환하는 기능을 구현했습니다. 실제 Windows PC에서 음소거, 음소거 해제, UI 상태 반영이 정상 동작하는 것을 수동으로 확인했습니다.

**Windows 마스터 음소거: 구현 완료 / 실제 PC 수동 검증 완료**

활성 애플리케이션 오디오 세션 조회와 앱별 음소거 토글을 구현했습니다. 실제 Windows PC에서 `suddenattack`과 Whale을 각각 음소거하고 해제했으며, 한 앱을 음소거해도 다른 앱과 Windows 마스터 오디오는 영향을 받지 않는 것을 확인했습니다.

**앱별 음소거: 구현 완료 / 실제 PC 수동 검증 완료**

Windows 마스터 오디오와 각 애플리케이션에 사용자 지정 전역 단축키를 설정·변경·삭제할 수 있습니다. 앱별 설정은 PID가 아니라 `ProcessName`으로 저장되므로 앱이 종료된 상태에서도 바인딩이 유지되고, 다시 실행되어 오디오 세션이 생기면 같은 설정을 사용할 수 있습니다. 설정은 `%LocalAppData%\MutePilot\settings.json`에 저장되며 MutePilot을 다시 실행할 때 복원됩니다.

전역 단축키는 MutePilot이 실행 중일 때만 동작합니다. 기존에는 단독 단축키를 F1~F11로 제한하고 modifier 조합도 영문·숫자 위주로 검사했지만, v0.7에서는 사용자가 입력 창에서 누른 키를 Windows virtual-key 값과 modifier flags로 표현하도록 바꿨습니다. 특정 키 목록을 늘리는 방식이 아니므로 W, Space, F8 같은 예시는 모두 같은 변환·검증 경로를 사용합니다.

Modifier가 없는 단축키는 사용자가 실제로 설정한 virtual-key만 약 15ms 간격의 단일 `GetAsyncKeyState` loop에서 확인합니다. 전체 키보드를 훑거나 다른 키 입력을 기록하지 않습니다. Ctrl/Alt/Shift/Win이 포함된 조합은 기존 `RegisterHotKey`와 `WM_HOTKEY` 경로를 사용하며 Windows 또는 다른 프로그램이 이미 쓰는 조합은 등록이 거부될 수 있습니다. 순수 modifier처럼 현재 구조에서 모호한 입력만 기술적 이유로 거부합니다.

MutePilot은 설정한 키를 관찰할 뿐 차단하거나 다른 입력으로 바꾸지 않습니다. 따라서 같은 키의 게임·편집기·브라우저 원래 동작도 그대로 전달됩니다. 관리자 권한 프로그램에서 단독 키를 확인하려면 MutePilot도 같은 권한 수준이 필요할 수 있습니다. 기존 F키와 modifier 설정은 이전 JSON의 `key` 값을 자동으로 virtual-key로 변환해 그대로 불러옵니다.

실제 PC에서 사용자가 직접 고른 단축키로 음소거와 볼륨 기능을 실행했고, SuddenAttack에서도 정상 동작하는 것을 확인했습니다. 단축키를 바꾸면 이전 키는 더 이상 동작하지 않고 새 키만 동작했으며, 앱을 다시 시작한 뒤에도 설정이 유지됐습니다. 메인 창을 트레이에 숨긴 상태의 단축키, 오버레이, 볼륨 프리셋 전환, 원래 볼륨과 원래 음소거 상태 복원도 함께 정상 동작했습니다.

**사용자 선택형 전역 단축키: 구현 및 실제 PC·SuddenAttack 수동 검증 완료**

오버레이를 켜면 주 모니터 오른쪽 위에 작은 고정형 HUD가 계속 표시됩니다. HUD에는 `Master`와 저장된 앱 단축키 대상만 나오며, 현재 오디오 세션이 없는 저장 앱은 `실행 안 됨`으로 표시됩니다. 음소거 버튼이나 전역 단축키로 상태를 바꾸면 해당 상태가 바로 반영되고, 저장 앱의 오디오 세션 시작·종료도 주기적으로 확인합니다.

전체 화면 앱이 foreground일 때 HUD는 마우스 클릭과 키보드 포커스를 가로채지 않는 display-only 상태입니다. 일반 Windows 화면에서는 각 HUD 행의 작은 speaker 버튼으로 Master 또는 실행 중인 앱만 음소거/해제할 수 있고, 오른쪽 위 `×`를 누르면 MutePilot은 종료하지 않은 채 Overlay만 OFF로 바뀝니다. 대시보드 빠른 버튼, 실행 설정, tray menu와 HUD 닫기 버튼은 모두 같은 `overlayEnabled`를 사용해 상태를 함께 갱신합니다. 설정은 `%LocalAppData%\MutePilot\settings.json`에 저장됩니다.

일반 Windows 화면에서는 HUD 오른쪽 위의 뚜렷한 자물쇠 버튼으로 위치 설정 모드를 바꿀 수 있습니다. 잠금을 풀면 `MutePilot` 제목 부분을 끌어 위치를 옮기고 20~100% 범위의 투명도 slider를 조절할 수 있습니다. 다시 잠그면 위치 이동과 설정 panel만 막히며 audio·접기·닫기 버튼은 계속 사용할 수 있습니다. `−`를 누르면 Overlay는 OFF로 바뀌지 않고 176×46 크기의 mini-HUD로 접히며, 펼치기 버튼으로 같은 HUD를 다시 복원합니다. 접힘 상태는 현재 실행 중에만 유지됩니다. 위치, 잠금 상태, 투명도는 기존 설정 파일에 계속 저장됩니다.

저장 위치가 현재 monitor 작업 영역 밖이면 가장 가까운 화면 안으로 되돌립니다. 메인 화면의 `오버레이 위치 초기화` 버튼으로 언제든 주 모니터 오른쪽 위 기본 위치로 복구할 수도 있습니다.

MutePilot은 약 400ms 간격으로 `GetForegroundWindow`, `GetWindowRect`, `MonitorFromWindow`, `GetMonitorInfo`를 사용해 foreground window가 monitor 전체를 덮는지 확인합니다. 전체 화면 중에는 사용자가 잠금을 풀어 둔 경우에도 저장 설정은 바꾸지 않고 잠금·투명도 control을 숨긴 display-only 상태로 전환해 모든 mouse 입력을 통과시킵니다. Alt+Tab으로 일반 화면에 돌아오면 기존 잠금 설정과 control을 복원합니다.

오버레이는 MutePilot이 별도의 WPF 창으로 표시하며 게임 프로세스에 접근하거나 화면에 코드를 주입하지 않습니다. 실제 SuddenAttack 환경에서 게임에 포커스가 있는 동안에도 고정형 HUD가 계속 보이고, Overlay ON/OFF와 기존 F8 앱별 음소거·해제가 함께 정상 동작하는 것을 수동으로 확인했습니다. 다만 일반 WPF 창 방식이므로 다른 독점 전체 화면 애플리케이션에서도 항상 표시된다고 보장할 수는 없습니다.

실제 PC에서 잠금 해제 상태의 위치 이동, 잠금 상태의 이동 차단, 투명도 조절, 위치·투명도·잠금 상태의 재시작 복원, 위치 초기화를 수동 확인했습니다. SuddenAttack이 foreground/fullscreen일 때는 HUD가 자동으로 display-only·click-through 상태가 되어 게임 중 조작되지 않았고, Alt+Tab 뒤에는 저장된 잠금 상태에 맞춰 설정 기능이 다시 제공됐습니다. 기존 SuddenAttack F8 음소거와 Overlay ON/OFF도 그대로 동작했습니다.

**음소거 상태 오버레이: 고정형 HUD 및 사용자 설정 실제 PC 수동 검증 완료**

메인 창의 X 버튼을 누르면 MutePilot은 종료되지 않고 시스템 트레이로 들어갑니다. 이때 기존 `MainWindow`와 audio, hotkey, overlay service를 그대로 유지하므로 설정된 단축키와 오버레이가 백그라운드에서도 계속 동작할 수 있습니다. 일반 최소화 버튼은 기존처럼 창을 최소화합니다.

트레이 메뉴의 `MutePilot 열기` 또는 아이콘 double-click으로 같은 메인 창을 다시 열 수 있습니다. `오버레이 켜기`/`오버레이 끄기`는 기존 `overlayEnabled` 설정을 저장하면서 HUD와 메인 화면의 ON/OFF 표시를 함께 갱신합니다. 프로그램을 완전히 끝내려면 트레이 메뉴의 `종료`를 사용해야 합니다. 처음 X 버튼을 누를 때만 트레이에서 계속 실행 중이라는 짧은 알림을 요청합니다.

실제 PC에서 X를 눌렀을 때 메인 창만 숨고 tray icon과 persistent HUD가 남는 것을 확인했습니다. 이 상태에서도 SuddenAttack 단독 F8 음소거·해제와 HUD 상태 갱신이 계속 동작했습니다. Tray icon double-click은 기존 메인 창을 복원했고, 반복 숨김·복원도 정상 동작했습니다. 트레이의 `종료`를 선택하면 프로세스와 HUD, tray icon이 모두 종료됐습니다.

**시스템 트레이 백그라운드 동작: 구현 및 실제 PC·SuddenAttack 수동 검증 완료**

메인 화면의 `실행 설정` 영역에서 `현재 실행 권한`을 `관리자 권한` 또는 `일반 권한`으로 확인할 수 있습니다. 상단 quick control은 `관리자모드 실행 ON/OFF`로 표시해 Windows 자체 설정처럼 보이지 않게 했습니다. 권한 변경은 MutePilot process 재시작으로 처리하며 Windows의 정상 `runas` UAC 흐름을 사용합니다. UAC를 우회하거나 관리자 자격 증명을 저장하지 않습니다.

`Windows 로그인 시 MutePilot 자동 실행`을 ON으로 바꾸면 시작프로그램 폴더나 `HKCU Run` 대신 현재 사용자 전용 Task Scheduler 작업 `MutePilot Startup`을 등록합니다. 이 작업은 현재 사용자가 로그인할 때 `Highest` run level과 `InteractiveToken`으로 현재 MutePilot 실행 파일에 `--background`를 전달합니다. SYSTEM 계정으로 실행하지 않습니다. OFF로 바꾸면 같은 작업을 제거하며, 생성·삭제가 실패하거나 UAC가 취소되면 실제 작업 상태를 다시 읽어 UI에 반영합니다.

`--background` 실행은 메인 창을 띄우지 않지만 tray icon, hotkey, 사용자 지정 단독 키 polling, audio service와 저장된 overlay 설정을 정상 초기화합니다. 사용자는 나중에 tray icon으로 같은 `MainWindow`를 열 수 있습니다. 사용자 SID와 Windows session ID가 포함된 named Mutex로 중복 실행을 막아 두 번째 background 실행은 조용히 끝나고, 두 번째 일반 실행은 이미 실행 중이라는 안내 후 종료됩니다.

관리자 재실행에서는 새 프로세스가 `--elevated-restart` handoff token으로 기존 Mutex가 풀리기를 기다립니다. UAC 승인이 끝나 새 프로세스 시작에 성공한 경우에만 기존 인스턴스를 정상 종료하므로, UAC를 취소하면 기존 MutePilot과 단일 실행 보호가 그대로 남습니다.

개발 중 자동 시작을 켜면 현재 `bin/Debug` 또는 `bin/Release` 실행 파일 경로가 그대로 등록됩니다. 빌드 위치를 옮기거나 파일을 지우면 작업이 유효하지 않을 수 있으므로 OFF/ON으로 다시 등록해야 합니다. 최종 배포 단계에서는 고정된 설치 경로를 마련할 예정입니다.

일반 GUI와 권한 표시뿐 아니라 정상 UAC를 거친 관리자 권한 재시작도 실제 Windows에서 확인했습니다. `MutePilot Startup` 작업을 켠 뒤 Windows를 재부팅하고 다시 로그인했을 때 MutePilot이 관리자 권한의 background/tray 상태로 자동 시작됐습니다. 자동 시작된 상태에서도 SuddenAttack 음소거 단축키와 볼륨 프리셋 toggle, tray에 숨긴 상태의 단축키, HUD 갱신이 정상 동작했습니다.

**Windows 자동 시작·관리자 권한·단일 실행: 구현 및 실제 재부팅·로그인 수동 검증 완료**

Master Audio와 저장된 각 애플리케이션에는 기존 음소거 단축키와 별도로 0~100% 범위의 볼륨 프리셋과 전용 단축키를 설정할 수 있습니다. 기존 v0.5에서는 버튼이나 단축키를 누를 때마다 고정값을 적용했지만, v0.6부터는 프리셋과 적용 직전의 실제 소리 상태를 오가는 toggle로 동작합니다. 첫 실행은 현재 볼륨과 음소거 여부를 기억한 뒤 프리셋을 적용하고 음소거를 해제하며, 두 번째 실행은 두 값을 함께 복원합니다.

여기서 **기본 볼륨은 프리셋을 적용하기 직전의 소리 상태입니다.** 영구적으로 정한 별도 숫자가 아닙니다. 예를 들어 앱이 73%·음소거 해제이고 프리셋이 10%라면 F2를 처음 눌렀을 때 10%·음소거 해제, 다시 누르면 73%·음소거 해제로 돌아옵니다. 복원 뒤 F2를 다시 누르면 그 시점의 실제 상태를 새 기본값으로 잡습니다. F1을 음소거 toggle, F2를 볼륨 프리셋 toggle로 설정한 경우에도 두 기능의 상태는 분리되므로 프리셋 사용 중 F1을 눌러 음소거한 뒤 F2로 원래 볼륨과 음소거 상태를 복원할 수 있습니다.

Master Audio는 현재 기본 Windows playback endpoint의 scalar volume과 장치 ID를 함께 확인합니다. 기본 장치가 바뀌면 이전 장치에서 잡은 기본 상태를 새 장치에 적용하지 않고 버립니다. 앱별 적용은 같은 `ProcessName`의 현재 오디오 session을 모두 프리셋 값으로 맞추지만, 복원할 때는 Windows가 제공하는 runtime session identity를 기준으로 각 session의 원래 볼륨과 음소거 상태를 따로 되돌립니다. 앱이 종료되거나 session 구성이 달라지면 이전 실행의 상태를 새 session에 적용하지 않습니다. 다른 애플리케이션 session은 변경하지 않습니다.

이 기본 상태는 실행 중인 메모리에만 있으며 `%LocalAppData%\MutePilot\settings.json`에는 저장하지 않습니다. 메인 창을 X로 숨겨 tray에서 계속 실행하는 동안에는 유지되지만, MutePilot을 실제로 종료하면 사라집니다. 다음 실행의 첫 프리셋 동작은 현재 상태를 새로 확인합니다. Slider 이동은 프리셋 설정만 저장하고 실제 오디오나 이미 잡아 둔 기본 상태를 바꾸지 않습니다. 실제 오디오는 `전환` 버튼이나 볼륨 단축키를 사용했을 때만 바뀝니다.

기존 `MasterHotkey`와 `ApplicationBindings[].Hotkey`는 그대로 음소거 단축키로 읽습니다. 새 `MasterVolumeHotkey`, `MasterVolumePercent`, `VolumeHotkey`, `VolumePercent`가 없는 기존 설정 파일은 볼륨 단축키가 없는 상태와 안전한 기본값 50%로 불러오며, 앱 시작만으로 파일을 다시 쓰거나 오디오를 바꾸지 않습니다. 음소거와 볼륨 action을 포함한 모든 MutePilot 단축키는 서로 중복될 수 없습니다.

메인 화면과 HUD에는 현재 볼륨이 표시됩니다. 여러 session의 값이 다르면 앱 목록에는 `혼합`으로 보이고, 프리셋 적용 뒤에는 지정한 퍼센트로 갱신됩니다. HUD는 음소거 해제 상태를 `🔊 25%`처럼 작게 표시하고 음소거 상태에서도 저장된 현재 볼륨을 함께 보여 줍니다.

현재 Windows에서 기존 형식의 설정 파일이 변경 없이 로드되는 것과 slider 편집만으로 실제 오디오가 바뀌지 않는 것을 다시 확인했습니다. 안전한 무음 test session으로 일반 복원, 원래 음소거 상태 복원, 프리셋 중 음소거 뒤 복원, slider 변경 뒤 기존 기본값 복원, 앱 session 재시작 시 이전 상태 폐기, 서로 다른 두 session의 정확한 개별 복원을 검증했습니다. X로 창을 숨긴 뒤에도 같은 프리셋 상태가 유지되어 전용 단축키로 원래 80%를 복원했고 HUD도 실제 상태를 갱신했습니다.

실제 SuddenAttack에서도 음소거 단축키와 볼륨 프리셋 toggle이 정상 동작했습니다. 프리셋 적용 뒤 원래 볼륨으로 돌아가는 것과 원래 음소거 상태까지 함께 복원되는 것을 확인했고, MutePilot을 tray에 숨긴 상태와 Windows 로그인 때 자동 시작된 관리자 권한 상태에서도 단축키와 HUD가 계속 동작했습니다.

**볼륨 프리셋 원래 상태 복원: 구현 및 SuddenAttack 수동 검증 완료**

## 앞으로 구현할 기능

* Windows 마스터 음소거/음소거 해제 — 구현 및 실제 동작 수동 검증 완료
* 활성 Windows 오디오 세션을 사용하는 애플리케이션 감지 — 구현 및 실제 세션 조회 확인 완료
* 애플리케이션별 음소거/음소거 해제 — 구현 및 실제 동작 수동 검증 완료
* 사용자가 직접 고른 키의 generic 전역 단축키와 토글 동작 — 구현 및 실제 PC·SuddenAttack 수동 검증 완료
* 여러 애플리케이션 단축키 바인딩 저장 — 구현 완료
* 프로그램을 다시 실행해도 유지되는 로컬 설정 — 구현 완료, 기본 설정 생성·재로드 확인
* 선택형 음소거 상태 오버레이 — 고정형 compact HUD 구현 및 SuddenAttack 수동 검증 완료
* 오버레이 위치 이동·잠금·20~100% 투명도 설정 — 구현 및 실제 PC 수동 검증 완료
* fullscreen foreground에서 자동 display-only·click-through 전환 — 구현 및 SuddenAttack 수동 검증 완료
* X 버튼으로 메인 창을 숨기고 시스템 트레이에서 계속 실행 — 구현 및 실제 PC 수동 검증 완료
* Task Scheduler 기반 Windows 자동 시작과 `--background` 실행 — 구현 및 재부팅·로그인 수동 검증 완료
* 관리자 권한 상태 표시와 명시적 관리자 재실행 — 구현 및 실제 UAC 수동 검증 완료
* 사용자 session별 단일 실행 보호 — 구현 및 중복 실행 검증 완료
* Master·애플리케이션별 볼륨 프리셋 toggle과 원래 상태 복원 — 구현 및 SuddenAttack 수동 검증 완료
* 사이드바 기반 최종 대시보드 UI와 다크·화이트·핑크 테마 — 구현 및 자동 GUI 검증 완료
* 테마 실시간 전환과 재시작 복원 — 구현 및 자동 GUI 검증 완료
* Master·앱별 0~100 숫자 프리셋 입력과 slider 동기화 — 구현 및 자동 GUI 검증 완료
* 대시보드·실행 설정 두 화면 탐색과 1100×740 기본 창 — 구현 및 실제 GUI 확인 완료
* 실행 중 앱 아이콘 추출·cache와 접근 실패 fallback — 구현 및 로컬 검증 완료
* 일반 화면에서 사용할 수 있는 Overlay 행별 audio 버튼·닫기 버튼 — 구현 및 Master audio 로컬 검증 완료
* 명확한 자물쇠 상태와 runtime mini-HUD 접기·복원 — 구현 및 로컬 GUI 검증 완료
* `Made by 유성우`와 분리된 `후원하기` 버튼, Toss QR과 계좌번호 복사 — 구현 및 로컬 GUI 검증 완료
* 실제 MutePilot 아이콘의 executable·MainWindow·taskbar·tray·Sidebar·About 연결 — 적용 및 로컬 GUI 검증 완료

연속적인 볼륨 증가·감소 조절은 현재 범위에 포함하지 않습니다. 볼륨 기능은 저장한 프리셋과 적용 직전의 실제 상태를 오가는 방식만 제공합니다.

## 사용 기술

* C# 12
* .NET 8
* WPF
* Windows Core Audio APIs
* NAudio.Wasapi 2.3.0
* Toss 제공 QR 이미지와 QRCoder 1.8.0 기반 로컬 fallback QR
* Windows `RegisterHotKey`, `UnregisterHotKey`, `WM_HOTKEY`
* Windows `GetAsyncKeyState` 기반 사용자 선택 단독 키 polling
* `System.Windows.Forms.NotifyIcon` 기반 시스템 트레이 메뉴
* Windows Task Scheduler COM API와 `runas` UAC 실행
* `WindowsIdentity`, `WindowsPrincipal` 기반 관리자 권한 확인
* 사용자 SID·session 기반 named Mutex 단일 실행 보호
* 포커스를 받지 않는 별도 WPF 상태 오버레이
* 교체 가능한 WPF `ResourceDictionary` 기반 3종 테마
* `%LocalAppData%`의 JSON 설정 저장

Windows API를 다루는 코드는 UI 코드와 분리하고, 필요한 기능부터 단순하게 구현할 계획입니다.

## 개발 로드맵

1. .NET 8 WPF 프로젝트와 초기 화면 구성 — 완료
2. Windows 마스터 음소거/음소거 해제 구현 — 완료, 실제 동작 수동 검증 완료
3. 활성 애플리케이션 오디오 세션 감지 — 완료, 실제 세션 조회 확인 완료
4. 애플리케이션별 음소거 토글 구현 — 완료, 실제 동작 수동 검증 완료
5. 사용자 지정 전역 단축키 추가 — 기존 F키·modifier 방식 구현 및 실제 동작 수동 검증 완료
6. 단축키 바인딩과 설정 저장 — 구현 완료
7. 실제 게임·브라우저 환경에서 단축키 수동 검증 — 완료, 관리자 게임 권한 제한 확인
8. 선택형 음소거 상태 오버레이 구현 — 고정형 HUD 개선 및 SuddenAttack 수동 검증 완료
9. 오버레이 위치·잠금·투명도 설정과 fullscreen display-only 처리 — 구현 및 SuddenAttack 수동 검증 완료
10. 시스템 트레이 백그라운드 동작 — 구현 및 SuddenAttack 수동 검증 완료
11. Windows 로그인 시 자동 실행, 관리자 권한 UX, background 시작, 단일 실행 보호 — 구현 및 실제 재부팅·로그인 수동 검증 완료
12. Master·앱별 볼륨 프리셋과 전용 단축키 — 구현 완료
13. 프리셋 적용 직전의 볼륨·음소거 상태 복원 — 구현 및 SuddenAttack 수동 검증 완료
14. 사용자가 직접 고른 키를 virtual-key 기반으로 저장·감시하는 구조 — 구현 및 실제 PC·SuddenAttack 수동 검증 완료
15. 사이드바 대시보드와 다크·화이트·핑크 테마 — 완료
16. Master·앱별 숫자 프리셋 입력과 slider 동기화 — 완료
17. 최종 아이콘 자산 적용 — 완료, GitHub Release 배포 구조 정리

## 작성자

**Made by 유성우**
