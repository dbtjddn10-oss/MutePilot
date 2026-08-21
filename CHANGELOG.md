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

마스터 음소거와 앱별 음소거 기능은 실제 Windows PC에서 수동 검증까지 완료했습니다. 전역 단축키의 빌드와 화면, 초기화는 확인했지만 실제 게임·브라우저 포커스 상태의 동작은 아직 수동 검증하지 않았습니다.
