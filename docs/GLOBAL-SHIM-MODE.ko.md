# v0.1.3 — 전체 게임 대상 Shim 모드 선택

## 변경 내용

기존 `settings.json`에 `ShimMode` 필드를 추가해 두 Shim 가운데 하나를 **모든 Game Bar 등록 게임에 일괄 적용**합니다.

- `ProcessorCountLie` (**기본**, 기존 동작 유지)
- `ProcessorCountLie2` (별도 선택, 시스템에 따라 게임 프로세스의 CPU Affinity를 제한할 수 있음)

`ProcessorCount` (기본 16) 값은 어느 모드에서도 그대로 사용합니다. 기존 `settings.json`에 `ShimMode`가 없으면 자동으로 `ProcessorCountLie`가 선택됩니다. 설정 값이 유효하지 않으면 조용히 다른 Shim을 적용하지 않고 오류를 표시합니다.

## 사용 방법

1. 관리자 권한으로 `C:\ProgramData\GameBarCpuProfile\settings.json`을 열어 `"ShimMode": "ProcessorCountLie2"`를 추가/변경합니다.
2. 관리자 PowerShell에서 `Restart-Service GameBarCpuProfile`을 실행합니다.
3. `C:\ProgramData\GameBarCpuProfile\x64.xml` / `x86.xml`의 `<SHIM NAME="ProcessorCountLie2" COMMAND_LINE="16" />`를 확인합니다(등록 게임이 있는 아키텍처만 해당).
4. `service.log`에 `INSTALLED x64 SDB: ... ProcessorCountLie2=16`이 기록되었는지 확인하고 게임을 **완전히 종료 후 재실행**합니다.
5. 되돌리려면 `ShimMode`를 `ProcessorCountLie`로 바꾸고 서비스를 재시작합니다.

## 변경하지 않은 것

- DB GUID, per-game GUID, `manifest.json` 구조 및 `settings.json`의 기존 필드
- Game Bar 이벤트 기반 감시, x64/x86 통합 SDB, 파일 크기 없는 EXE 이름 매칭
- 게임별 `--exclude` / `--include`, 설치/제거 동작

## 주의

`ProcessorCountLie2`는 사용자 테스트에서 프로세스 Affinity까지 변경했으나, 공식 문서로 내부 메커니즘을 단정할 수 없습니다. Affinity 제한은 **게임이 백그라운드일 때도 유지될 수 있으며**, CCD 번호 배치는 하드웨어·BIOS·Windows 설정에 따라 달라집니다. 모든 게임에 일괄 적용하는 경우, 효과가 없거나 성능이 떨어지는 게임도 생길 수 있습니다. 안티치트 게임과 AMD/Windows SDB에서 이미 동일 EXE에 Fix를 설정한 게임은 특히 주의하세요.

**테스트 안내:** 이 저장소는 Linux 환경에서 소스 수정·패키징되었으며 Windows 서비스 및 ADK의 실제 컴파일/설치 테스트는 수행되지 않았습니다.
