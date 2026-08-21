# MutePilot

## MutePilot 소개

MutePilot은 사용자가 지정한 전역 단축키로 Windows의 마스터 오디오와 개별 애플리케이션 오디오 세션을 음소거하거나 다시 켤 수 있도록 만드는 가벼운 Windows 유틸리티입니다.

## 만들게 된 이유

게임이나 작업 중에 여러 창을 오가지 않고 필요한 소리만 빠르게 제어하는 도구를 직접 만들어 보기 위해 시작했습니다. Windows 오디오 API와 전역 단축키가 실제 데스크톱 프로그램에서 어떻게 연결되는지 배우는 과정도 함께 기록합니다.

## 현재 개발 상태

현재는 **v0.3 기능 개발 단계**입니다.

.NET 8 WPF 앱에서 Windows 기본 출력 장치의 마스터 음소거 상태를 읽고, 버튼으로 음소거와 음소거 해제를 전환하는 기능을 구현했습니다. 실제 Windows PC에서 음소거, 음소거 해제, UI 상태 반영이 정상 동작하는 것을 수동으로 확인했습니다.

**Windows 마스터 음소거: 구현 완료 / 실제 PC 수동 검증 완료**

활성 애플리케이션 오디오 세션 조회와 앱별 음소거 토글을 구현했습니다. 실제 Windows PC에서 `suddenattack`과 Whale을 각각 음소거하고 해제했으며, 한 앱을 음소거해도 다른 앱과 Windows 마스터 오디오는 영향을 받지 않는 것을 확인했습니다.

**앱별 음소거: 구현 완료 / 실제 PC 수동 검증 완료**

Windows 마스터 오디오와 각 애플리케이션에 사용자 지정 전역 단축키를 설정·변경·삭제할 수 있습니다. 앱별 설정은 PID가 아니라 `ProcessName`으로 저장되므로 앱이 종료된 상태에서도 바인딩이 유지되고, 다시 실행되어 오디오 세션이 생기면 같은 설정을 사용할 수 있습니다. 설정은 `%LocalAppData%\MutePilot\settings.json`에 저장되며 MutePilot을 다시 실행할 때 복원됩니다.

전역 단축키는 MutePilot이 실행 중일 때만 동작합니다. Whale 전역 단축키와 SuddenAttack foreground의 `Ctrl + Alt + F8`을 수동 검증했습니다. 단독 F키 polling은 일반 권한으로 실행한 MutePilot에서는 관리자 권한 SuddenAttack의 F7/F8 상태를 읽지 못했지만, MutePilot도 관리자 권한으로 실행했을 때 Master Audio의 F7과 SuddenAttack의 F8이 정상 동작했습니다. F8을 누르고 있어도 한 번만 전환되는 것도 확인했습니다.

Ctrl/Alt/Shift가 포함된 조합은 Windows `RegisterHotKey`를 사용합니다. F1~F11 단독 단축키는 현재 설정된 키만 `GetAsyncKeyState`로 확인하며, foreground 프로그램에도 같은 F키 입력은 그대로 전달됩니다. 게임 자체 단축키와 충돌한다면 modifier 조합을 사용하는 편이 안전합니다. 관리자 권한으로 실행되는 게임에서 단독 F키를 사용하려면 MutePilot도 관리자 권한 실행이 필요할 수 있습니다. MutePilot을 항상 관리자 권한으로 실행해야 하는 것은 아닙니다.

**사용자 지정 전역 단축키: 구현 완료 / 실제 PC 수동 검증 완료 / 관리자 게임 권한 제한 확인**

오버레이를 켜면 주 모니터 오른쪽 위에 작은 고정형 HUD가 계속 표시됩니다. HUD에는 `Master`와 저장된 앱 단축키 대상만 나오며, 현재 오디오 세션이 없는 저장 앱은 `실행 안 됨`으로 표시됩니다. 음소거 버튼이나 전역 단축키로 상태를 바꾸면 해당 상태가 바로 반영되고, 저장 앱의 오디오 세션 시작·종료도 주기적으로 확인합니다.

HUD는 마우스 클릭과 키보드 포커스를 가로채지 않으며, 메인 화면의 `오버레이` 버튼을 OFF로 바꾸면 즉시 완전히 숨습니다. 설정은 `%LocalAppData%\MutePilot\settings.json`의 `overlayEnabled`에 저장되고, 기존 설정 파일에 이 항목이 없으면 기본값인 ON으로 시작합니다.

일반 Windows 화면에서는 HUD 오른쪽 위의 잠금 버튼으로 설정 모드를 바꿀 수 있습니다. 잠금을 풀면 `MutePilot` 제목 부분을 끌어 위치를 옮기고 20~100% 범위의 투명도 slider를 조절할 수 있습니다. 다시 잠그면 본문은 click-through 상태가 되고 위치를 움직일 수 없지만 잠금 버튼은 남아 있어 다시 설정할 수 있습니다. 위치, 잠금 상태, 투명도는 기존 설정 파일의 `overlayLeft`, `overlayTop`, `overlayLocked`, `overlayOpacity`에 함께 저장됩니다.

저장 위치가 현재 monitor 작업 영역 밖이면 가장 가까운 화면 안으로 되돌립니다. 메인 화면의 `오버레이 위치 초기화` 버튼으로 언제든 주 모니터 오른쪽 위 기본 위치로 복구할 수도 있습니다.

MutePilot은 약 400ms 간격으로 `GetForegroundWindow`, `GetWindowRect`, `MonitorFromWindow`, `GetMonitorInfo`를 사용해 foreground window가 monitor 전체를 덮는지 확인합니다. 전체 화면 중에는 사용자가 잠금을 풀어 둔 경우에도 저장 설정은 바꾸지 않고 잠금·투명도 control을 숨긴 display-only 상태로 전환해 모든 mouse 입력을 통과시킵니다. Alt+Tab으로 일반 화면에 돌아오면 기존 잠금 설정과 control을 복원합니다.

오버레이는 MutePilot이 별도의 WPF 창으로 표시하며 게임 프로세스에 접근하거나 화면에 코드를 주입하지 않습니다. 실제 SuddenAttack 환경에서 게임에 포커스가 있는 동안에도 고정형 HUD가 계속 보이고, Overlay ON/OFF와 기존 F8 앱별 음소거·해제가 함께 정상 동작하는 것을 수동으로 확인했습니다. 다만 일반 WPF 창 방식이므로 다른 독점 전체 화면 애플리케이션에서도 항상 표시된다고 보장할 수는 없습니다.

실제 PC에서 잠금 해제 상태의 위치 이동, 잠금 상태의 이동 차단, 투명도 조절, 위치·투명도·잠금 상태의 재시작 복원, 위치 초기화를 수동 확인했습니다. SuddenAttack이 foreground/fullscreen일 때는 HUD가 자동으로 display-only·click-through 상태가 되어 게임 중 조작되지 않았고, Alt+Tab 뒤에는 저장된 잠금 상태에 맞춰 설정 기능이 다시 제공됐습니다. 기존 SuddenAttack F8 음소거와 Overlay ON/OFF도 그대로 동작했습니다.

**음소거 상태 오버레이: 고정형 HUD 및 사용자 설정 실제 PC 수동 검증 완료**

메인 창의 X 버튼을 누르면 MutePilot은 종료되지 않고 시스템 트레이로 들어갑니다. 이때 기존 `MainWindow`와 audio, hotkey, overlay service를 그대로 유지하므로 설정된 단축키와 오버레이가 백그라운드에서도 계속 동작할 수 있습니다. 일반 최소화 버튼은 기존처럼 창을 최소화합니다.

트레이 메뉴의 `MutePilot 열기` 또는 아이콘 double-click으로 같은 메인 창을 다시 열 수 있습니다. `오버레이 켜기`/`오버레이 끄기`는 기존 `overlayEnabled` 설정을 저장하면서 HUD와 메인 화면의 ON/OFF 표시를 함께 갱신합니다. 프로그램을 완전히 끝내려면 트레이 메뉴의 `종료`를 사용해야 합니다. 처음 X 버튼을 누를 때만 트레이에서 계속 실행 중이라는 짧은 알림을 요청합니다.

실제 Windows UI에서 X 이후 프로세스와 HUD가 남는 것, 메뉴와 double-click 복원, 반복 숨김·복원 중 단일 tray icon 유지, 오버레이 설정 동기화, 명시적 종료와 재실행을 확인했습니다. 트레이 상태에서 SuddenAttack 단독 F8을 사용하는 실제 게임 테스트는 아직 하지 않았습니다.

**시스템 트레이 백그라운드 동작: 구현 및 Windows UI 검증 완료 / 게임 단축키 수동 검증 필요**

## 앞으로 구현할 기능

* Windows 마스터 음소거/음소거 해제 — 구현 및 실제 동작 수동 검증 완료
* 활성 Windows 오디오 세션을 사용하는 애플리케이션 감지 — 구현 및 실제 세션 조회 확인 완료
* 애플리케이션별 음소거/음소거 해제 — 구현 및 실제 동작 수동 검증 완료
* 사용자 지정 전역 단축키와 토글 동작 — 구현 및 실제 PC 수동 검증 완료
* 여러 애플리케이션 단축키 바인딩 저장 — 구현 완료
* 프로그램을 다시 실행해도 유지되는 로컬 설정 — 구현 완료, 기본 설정 생성·재로드 확인
* 선택형 음소거 상태 오버레이 — 고정형 compact HUD 구현 및 SuddenAttack 수동 검증 완료
* 오버레이 위치 이동·잠금·20~100% 투명도 설정 — 구현 및 실제 PC 수동 검증 완료
* fullscreen foreground에서 자동 display-only·click-through 전환 — 구현 및 SuddenAttack 수동 검증 완료
* X 버튼으로 메인 창을 숨기고 시스템 트레이에서 계속 실행 — 구현 및 Windows UI 검증 완료

오디오 볼륨 조절 기능은 현재 범위에 포함하지 않습니다.

## 사용 기술

* C# 12
* .NET 8
* WPF
* Windows Core Audio APIs
* NAudio.Wasapi 2.3.0
* Windows `RegisterHotKey`, `UnregisterHotKey`, `WM_HOTKEY`
* Windows `GetAsyncKeyState` 기반 단독 F키 polling
* `System.Windows.Forms.NotifyIcon` 기반 시스템 트레이 메뉴
* 포커스를 받지 않는 별도 WPF 상태 오버레이
* `%LocalAppData%`의 JSON 설정 저장

Windows API를 다루는 코드는 UI 코드와 분리하고, 필요한 기능부터 단순하게 구현할 계획입니다.

## 개발 로드맵

1. .NET 8 WPF 프로젝트와 초기 화면 구성 — 완료
2. Windows 마스터 음소거/음소거 해제 구현 — 완료, 실제 동작 수동 검증 완료
3. 활성 애플리케이션 오디오 세션 감지 — 완료, 실제 세션 조회 확인 완료
4. 애플리케이션별 음소거 토글 구현 — 완료, 실제 동작 수동 검증 완료
5. 사용자 지정 전역 단축키 추가 — 구현 및 실제 동작 수동 검증 완료
6. 단축키 바인딩과 설정 저장 — 구현 완료
7. 실제 게임·브라우저 환경에서 단축키 수동 검증 — 완료, 관리자 게임 권한 제한 확인
8. 선택형 음소거 상태 오버레이 구현 — 고정형 HUD 개선 및 SuddenAttack 수동 검증 완료
9. 오버레이 위치·잠금·투명도 설정과 fullscreen display-only 처리 — 구현 및 SuddenAttack 수동 검증 완료
10. 시스템 트레이 백그라운드 동작 — 구현 및 Windows UI 검증 완료
11. Windows 시작 시 자동 실행 및 관리자 권한 실행 방식 정리

## 작성자

**Made by 유성우**
