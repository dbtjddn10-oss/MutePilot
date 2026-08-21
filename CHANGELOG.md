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

마스터·앱별 음소거와 Whale 전역 단축키를 실제 Windows PC에서 수동 검증했습니다. SuddenAttack foreground에서는 `Ctrl + Alt + F8`이 일반 권한에서도 동작했고, 단독 F7/F8은 MutePilot과 게임의 관리자 권한 수준을 맞췄을 때 동작했습니다. F8을 누르고 있어도 한 번만 전환되는 repeat prevention도 확인했습니다.

고정형 HUD가 2.3초 뒤에도 유지되는 것과 click-through, focus 유지, ON/OFF, 설정 복원, 같은 window의 상태 갱신을 Windows 환경에서 검증했습니다. 이전 팝업형 오버레이는 SuddenAttack에서도 표시되는 것을 사용자가 확인했습니다. 고정형 HUD는 실제 게임에서 다시 확인할 예정이며, 독점 전체 화면 위 표시는 일반 WPF 창 방식으로 보장되지 않습니다.
