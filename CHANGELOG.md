# 변경 기록

MutePilot에서 실제로 완료한 주요 변경 사항을 기록합니다.

## [Unreleased]

### 추가

* 초기 프로젝트 README와 개발 로드맵 작성
* 개발 범위, 문서 작성, 검증, Git 작업 규칙을 담은 `AGENTS.md` 추가
* GitHub 문서를 자연스러운 한국어로 작성하는 규칙 추가
* .NET 8 WPF 솔루션과 `MutePilot` 프로젝트 생성
* `Master Audio`, `Application Shortcuts` placeholder와 작성자 표기가 있는 초기 메인 화면 구성
* 제품명 `MutePilot`, 버전 `0.1.0`, 대상 프레임워크 `net8.0-windows` 설정
* Windows 기본 출력 장치의 마스터 음소거 상태 조회, 설정, 토글 기능 구현
* 현재 음소거 상태와 동작 버튼을 연결하고 오디오 제어 실패 메시지를 표시하도록 `Master Audio` UI 변경
* Windows Core Audio endpoint 제어를 위해 `NAudio.Wasapi 2.3.0` 추가
* 실제 Windows PC에서 마스터 음소거, 음소거 해제, UI 상태 반영을 수동 검증
* 기본 출력 장치의 활성 애플리케이션 오디오 세션 조회 기능 구현
* `ProcessName` 기준으로 같은 애플리케이션의 여러 session을 묶고 PID, session 수, mute 상태를 표시
* 애플리케이션 그룹의 session만 음소거하거나 해제하는 토글 기능 구현
* `Applications` 목록과 수동 `새로고침` 버튼 추가
* 실제 Windows PC에서 `suddenattack`과 Whale의 개별 음소거·해제, 앱 간 독립 제어, master audio 유지 동작을 수동 검증
* Windows `RegisterHotKey`, `UnregisterHotKey`, `WM_HOTKEY`를 이용한 전체·앱별 전역 단축키 등록과 토글 연결
* F1~F11 단독 키와 Ctrl/Alt/Shift를 조합한 영문·숫자 키 입력 창 추가
* MutePilot 내부 중복과 Windows 단축키 등록 충돌을 확인하고 기존 바인딩을 보존하는 변경 처리 추가
* 앱이 실행 중이 아니어도 `ProcessName` 기준 바인딩을 유지하고 목록에서 변경·삭제할 수 있도록 구성
* `%LocalAppData%\MutePilot\settings.json`에 단축키를 저장하고 시작할 때 개별적으로 복원하는 기능 추가
* fullscreen 프로그램이 foreground인 동안 단독 F1~F11 상태를 확인할 수 있도록 설정된 키만 조회하는 `GetAsyncKeyState` polling 경로 추가
* 단독 F키는 polling만 사용하고 modifier 조합은 기존 `RegisterHotKey`를 사용하도록 분리해 이중 토글 방지
* `GetAsyncKeyState`의 high-order bit와 key latch로 down 전환을 구분해 키를 누르고 있을 때 반복 토글되지 않도록 처리
* 단축키 입력 창의 표시 영역 높이와 padding을 늘려 단축키 문자열이 잘리던 문제 수정
* 마스터·앱별 음소거 상태를 주 모니터 오른쪽 위에 계속 표시하는 선택형 WPF HUD 추가
* 일시 팝업형 오버레이를 `Master`와 저장된 앱 바인딩만 보여 주는 작은 고정형 HUD로 변경
* 저장 앱의 활성 오디오 세션이 없으면 `실행 안 됨`으로 표시하고, 세션 시작·종료를 주기적으로 갱신하도록 구성
* 버튼, 전역 단축키, 새로고침 뒤에 같은 HUD window의 상태 snapshot을 즉시 갱신하도록 연결
* 저장된 앱이 많을 때 최대 7개 앱 행과 나머지 개수만 표시해 HUD 높이를 제한
* 오버레이가 포커스를 받거나 마우스 입력을 가로채지 않도록 `WS_EX_NOACTIVATE`, `WS_EX_TRANSPARENT`, `WS_EX_TOOLWINDOW` 확장 스타일 적용
* 메인 화면에서 오버레이를 즉시 켜고 끄는 설정과 `%LocalAppData%\MutePilot\settings.json`의 `overlayEnabled` 저장·복원 추가
* 기존 설정 파일에 `overlayEnabled`가 없을 때 기본값 ON을 사용하도록 이전 설정과의 호환성 유지
* 잠금 해제 상태에서 제목 영역을 drag해 HUD 위치를 옮기고 종료 뒤에도 복원하는 기능 추가
* HUD 상태 card의 투명도를 20~100% 범위에서 조절하는 compact slider 추가
* `overlayLocked`, `overlayOpacity`, `overlayLeft`, `overlayTop` 설정을 기존 JSON에 호환되는 방식으로 추가
* 저장 좌표가 화면 밖이면 가장 가까운 monitor 작업 영역으로 되돌리는 위치 안전 처리 추가
* 메인 화면에 HUD를 주 모니터 오른쪽 위로 되돌리는 `오버레이 위치 초기화` 버튼 추가
* foreground window와 monitor 크기를 약 400ms마다 비교하는 generic fullscreen 감지 추가
* fullscreen 중에는 저장된 잠금 설정을 바꾸지 않고 control을 숨기며 `WS_EX_TRANSPARENT` click-through를 강제하도록 변경
* 일반 화면의 잠금 상태에서는 HUD 본문 click을 통과시키면서 잠금 버튼만 사용할 수 있도록 `WM_NCHITTEST` 처리 추가
* `System.Windows.Forms.NotifyIcon`을 이용한 시스템 트레이 아이콘과 `MutePilot 열기`, 오버레이 ON/OFF, `종료` 메뉴 추가
* 메인 창의 X 버튼을 누르면 종료 대신 기존 창을 숨기고, 처음 한 번만 트레이 실행 안내를 요청하도록 변경
* `ShutdownMode="OnExplicitShutdown"`과 실제 종료 flag를 사용해 트레이 `종료`에서만 hotkey, polling, fullscreen monitor, overlay, tray icon을 정리하도록 구성
* 트레이 메뉴와 icon double-click에서 새 창을 만들지 않고 기존 `MainWindow`를 복원하도록 연결
* 트레이 오버레이 메뉴가 기존 `overlayEnabled`를 저장하고 HUD와 메인 화면 ON/OFF 표시를 함께 갱신하도록 구성

마스터·앱별 음소거와 Whale 전역 단축키를 실제 Windows PC에서 수동 검증했습니다. SuddenAttack foreground에서는 `Ctrl + Alt + F8`이 일반 권한에서도 동작했고, 단독 F7/F8은 MutePilot과 게임의 관리자 권한 수준을 맞췄을 때 동작했습니다. F8을 누르고 있어도 한 번만 전환되는 repeat prevention도 확인했습니다.

고정형 HUD가 2.3초 뒤에도 유지되는 것과 click-through, focus 유지, ON/OFF, 설정 복원, 같은 window의 상태 갱신을 Windows 환경에서 검증했습니다. 실제 SuddenAttack에서도 게임에 포커스가 있는 동안 HUD가 계속 보이고, Overlay ON/OFF와 기존 F8 앱별 음소거·해제가 정상 동작하는 것을 수동으로 확인했습니다. 다른 독점 전체 화면 애플리케이션 위의 표시는 일반 WPF 창 방식으로 보장되지 않습니다.

위치 이동과 drag 완료 좌표 전달, 잠금 시 이동 차단, 20%·100% 투명도, 화면 밖 좌표 복구, 재시작 위치 복원, 위치 초기화, fullscreen display-only 자동 전환과 복귀를 검증했습니다. 실제 PC에서도 위치 이동, 잠금, 투명도, 위치 초기화와 각 설정의 재시작 복원을 확인했습니다. SuddenAttack foreground/fullscreen에서는 HUD가 자동으로 display-only·click-through 상태가 되어 조작되지 않았고, Alt+Tab 뒤에는 저장된 잠금 상태에 맞춰 설정 기능이 복원됐습니다. 기존 F8 음소거와 Overlay ON/OFF도 그대로 동작했습니다.

시스템 트레이 기능은 실제 Windows UI에서 main window 숨김 뒤 process와 overlay 유지, 메뉴·double-click 복원, 반복 hide/show 중 단일 icon 유지, 오버레이 설정과 메인 UI 동기화, 명시적 종료 시 모든 window와 icon 제거, 정상 재실행을 확인했습니다. 트레이 상태의 SuddenAttack 단독 F8은 아직 실제 게임에서 수동 검증하지 않았습니다.
