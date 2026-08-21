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
* 메인 화면에 실제 Task Scheduler 상태와 현재 관리자 권한 여부를 보여 주는 compact `Windows 시작` 영역 추가
* 현재 사용자 로그인 trigger, `Highest` run level, `InteractiveToken`, `--background` action을 사용하는 `MutePilot Startup` 작업 등록·제거 기능 추가
* Task Scheduler 작업 변경과 관리자 재실행에 Windows `runas` UAC 흐름을 사용하고 취소·실패 시 기존 앱과 실제 상태를 유지하도록 처리
* `--background`에서 메인 창 없이 tray, hotkey, 단독 F키 polling, audio, overlay service를 초기화하도록 앱 시작 경로 변경
* `WindowsIdentity`와 `WindowsPrincipal`로 현재 process의 관리자 권한 상태를 표시하고 같은 실행 파일을 관리자 권한으로 다시 시작하는 기능 추가
* 사용자 SID와 session ID 기반 named Mutex로 중복 process, tray icon, hotkey 등록을 막는 단일 실행 보호 추가
* 관리자 재실행 process가 기존 Mutex 해제를 기다리는 `--elevated-restart` handoff 경로 추가
* Master Audio와 각 저장 애플리케이션에 1~100% 볼륨 프리셋, 전용 단축키, 수동 `적용` 기능 추가
* `HotkeyActionType`과 action별 binding ID를 도입해 한 대상에 음소거와 볼륨 단축키를 함께 등록하도록 변경
* 기존 `MasterHotkey`, `ApplicationBindings[].Hotkey`를 음소거 단축키로 유지하면서 선택형 볼륨 단축키와 기본 50% 값을 JSON schema에 호환 방식으로 추가
* 기본 playback endpoint의 master scalar volume 조회·설정과 `ProcessName`이 같은 모든 활성 audio session의 볼륨 일괄 설정 추가
* 볼륨 프리셋 적용 시 해당 대상을 음소거 해제하고 실제 결과를 다시 읽어 UI와 HUD에 반영
* 여러 앱 session의 현재 볼륨이 다르면 `혼합`으로 표시하고 HUD에 음소거·볼륨 상태를 compact하게 함께 표시
* 음소거와 볼륨 action을 포함한 모든 MutePilot 단축키 사이의 중복 검사 추가
* Master Audio의 기본 장치 ID·볼륨·음소거 상태와 앱별 runtime session identity·볼륨·음소거 상태를 실행 중에만 보관하는 프리셋 toggle service 추가
* 같은 앱의 여러 오디오 session이 서로 다른 볼륨·음소거 상태였을 때 session별 원래 상태를 정확히 복원하는 처리 추가
* 앱 session 구성이 달라지거나 기본 playback endpoint가 바뀌면 이전 기본 상태를 새 대상에 적용하지 않는 무효화 처리 추가
* 사용자가 입력한 키를 localized 표시 문자열이 아닌 Windows virtual-key와 modifier flags로 저장하는 generic hotkey 표현 추가
* 이전 JSON의 WPF `key` 값을 virtual-key로 변환해 기존 F키와 modifier 단축키를 그대로 불러오는 호환 converter 추가

### 변경

* 시작·권한 영역 제목을 `실행 설정`으로 바꾸고 자동 실행 문구를 `Windows 로그인 시 MutePilot 자동 실행`으로 명확히 수정
* 관리자 재실행 버튼 문구를 `MutePilot을 관리자 권한으로 재시작`으로 바꿔 Windows 재시작과 혼동하지 않도록 개선
* 볼륨 프리셋 동작을 매번 고정값을 적용하던 방식에서 프리셋과 적용 직전의 실제 볼륨·음소거 상태를 오가는 방식으로 변경
* 프리셋 수동 버튼과 전용 단축키가 같은 runtime 기본 상태를 공유하도록 연결하고, 활성 상태의 버튼 문구를 `기본 볼륨으로 복원`으로 변경
* 프리셋 활성 중 slider를 바꿔도 실제 오디오와 이미 저장한 기본 상태는 유지하고, 복원 뒤 다음 주기부터 새 프리셋 값을 사용하도록 변경
* 단독 단축키의 F1~F11 제한과 modifier 조합의 영문·숫자 whitelist를 제거하고 입력 창에서 사용자가 누른 일반 키를 같은 경로로 처리하도록 변경
* 단독 키 polling을 함수 키 전용 분기에서 현재 등록된 virtual-key만 조회하는 generic map으로 변경
* Escape를 입력 창 취소 전용 키로 쓰지 않고 일반 단축키로 선택할 수 있게 하며, 취소는 명시적인 `취소` 버튼으로 유지

마스터·앱별 음소거와 Whale 전역 단축키를 실제 Windows PC에서 수동 검증했습니다. SuddenAttack foreground에서는 `Ctrl + Alt + F8`이 일반 권한에서도 동작했고, 단독 F7/F8은 MutePilot과 게임의 관리자 권한 수준을 맞췄을 때 동작했습니다. F8을 누르고 있어도 한 번만 전환되는 repeat prevention도 확인했습니다.

고정형 HUD가 2.3초 뒤에도 유지되는 것과 click-through, focus 유지, ON/OFF, 설정 복원, 같은 window의 상태 갱신을 Windows 환경에서 검증했습니다. 실제 SuddenAttack에서도 게임에 포커스가 있는 동안 HUD가 계속 보이고, Overlay ON/OFF와 기존 F8 앱별 음소거·해제가 정상 동작하는 것을 수동으로 확인했습니다. 다른 독점 전체 화면 애플리케이션 위의 표시는 일반 WPF 창 방식으로 보장되지 않습니다.

위치 이동과 drag 완료 좌표 전달, 잠금 시 이동 차단, 20%·100% 투명도, 화면 밖 좌표 복구, 재시작 위치 복원, 위치 초기화, fullscreen display-only 자동 전환과 복귀를 검증했습니다. 실제 PC에서도 위치 이동, 잠금, 투명도, 위치 초기화와 각 설정의 재시작 복원을 확인했습니다. SuddenAttack foreground/fullscreen에서는 HUD가 자동으로 display-only·click-through 상태가 되어 조작되지 않았고, Alt+Tab 뒤에는 저장된 잠금 상태에 맞춰 설정 기능이 복원됐습니다. 기존 F8 음소거와 Overlay ON/OFF도 그대로 동작했습니다.

시스템 트레이 기능은 실제 PC에서 X를 누른 뒤 main window만 숨고 process, tray icon, persistent HUD가 남는 것을 확인했습니다. 숨겨진 상태에서도 SuddenAttack 단독 F8 음소거·해제와 HUD 갱신이 계속 동작했습니다. Icon double-click 복원, 반복 hide/show, 트레이 `종료` 시 process·overlay·icon 정리도 정상 동작했습니다.

일반 GUI의 startup UI와 권한 표시, `--background` 창 숨김, overlay OFF/ON 복원, normal/background 중복 실행 종료, Mutex handoff 뒤 단일 process·tray 유지를 검증했습니다. 이후 실제 PC에서 관리자 권한 재시작과 `Highest` 자동 시작 작업을 확인했고, Windows 재부팅·로그인 뒤 MutePilot이 관리자 권한의 background/tray 상태로 자동 실행되는 것도 수동 검증했습니다.

안전한 무음 Windows 오디오 session으로 일반 상태와 원래 음소거 상태 복원, 프리셋 중 별도 음소거, 활성 중 slider 변경, session 재시작, 서로 다른 두 session의 개별 복원을 확인했습니다. 실제 SuddenAttack에서도 음소거 단축키, 볼륨 프리셋 toggle, 원래 볼륨과 원래 음소거 상태 복원이 정상 동작했습니다. Tray에 숨긴 상태와 자동 시작된 관리자 권한 MutePilot에서도 단축키가 동작하고 HUD가 실제 상태를 갱신하는 것을 수동 확인했습니다.

W, S, Space, Escape와 modifier 조합이 같은 virtual-key 표현으로 만들어지는 것을 자동 확인했습니다. 중복 거부, 단독 키 변경·삭제 시 polling map과 latch 정리, 하나의 polling task 유지, 실제 `RegisterHotKey` 경로, 이전 F8 JSON 호환도 검증했습니다.

실제 PC와 SuddenAttack에서 사용자가 직접 고른 단축키가 정상 동작하는 것을 수동 확인했습니다. 트레이에 숨긴 상태의 단축키와 오버레이, 볼륨 프리셋 전환, 원래 볼륨·음소거 상태 복원이 유지됐습니다. 단축키 변경 뒤에는 이전 키가 멈추고 새 키만 동작했으며, MutePilot 재시작 뒤에도 설정이 복원됐습니다.
