# GameBar CPU Profile — 개발자 가이드

Xbox Game Bar에 등록된 EXE를 감지하여, Windows Application Compatibility의
`ProcessorCountLie=16`(기본) 또는 `ProcessorCountLie2=16`(선택) 설정을 아키텍처별 통합 `.sdb` 2개에 자동 반영하는 서비스입니다.

**참고:** 초기 소스는 Windows 실기기에서 컴파일·설치 검증을 거치기 전 작성되었습니다.
릴리스 상태는 GitHub Release 및 실제 테스트 결과를 기준으로 확인하세요.

## 구성

- `src/GameBarCpuProfile.Service/`: .NET 10 Windows Service 소스
- `installer/GameBarCpuProfile.iss`: Inno Setup 6 설치 관리자
- `scripts/build-installer.ps1`: 개발자용 빌드 스크립트
- `publish/`: `dotnet publish` 결과 (Git에는 포함하지 않음)
- `installer/Output/`: 완성된 설치 파일 (Git에는 포함하지 않음)

## 개발 환경 (Windows 11 + VS Code)

1. Microsoft .NET **10 SDK** 설치.
2. VS Code + Microsoft **C# Dev Kit** 확장 설치.
3. **Inno Setup 6** 설치.
4. Windows ADK의 **Application Compatibility Tools** 설치. `fixApp.exe`가 필요합니다.
5. 이 폴더를 VS Code로 열고 터미널에서:

```powershell
dotnet restore .\src\GameBarCpuProfile.Service\GameBarCpuProfile.Service.csproj
dotnet build .\src\GameBarCpuProfile.Service\GameBarCpuProfile.Service.csproj
```

개발 중 콘솔에서 실행하려면 **관리자 권한**으로 (아직 서비스 설치 전):

```powershell
dotnet run --project .\src\GameBarCpuProfile.Service -- --console
```

Ctrl+C로 종료합니다. 실제 게임 프로세스는 폴링하지 않습니다.

## Windows EXE와 설치파일 만들기

```powershell
.\scripts\build-installer.ps1
```

성공 시 생성 파일:

- `publish/GameBarCpuProfile.Service.exe` — **자체 포함 단일 파일 EXE**
- `installer/Output/GameBarCpuProfile-Setup-0.1.3-x64.exe` — 설치 관리자

사용자는 설치 프로그램을 관리자 권한으로 실행합니다. 설치 관리자가
`GameBarCpuProfile` 서비스를 자동 시작(지연 시작)으로 등록합니다.
`services.msc`에서 **GameBar CPU Profile Service**로 표시됩니다.

### 설치 전 기존 PowerShell 버전 정리

기존에 설치된 `GameBar ProcessorCountLie` 또는 `GameBar CPU Profile`의 SDB가
남아 있으면 **동일 게임에 Shim이 중복 적용될 수 있습니다.**

기존 PowerShell EventWatch를 종료한 다음, **기존 스크립트의**
`-UninstallAll -Apply`로 해당 SDB를 제거하세요. 기존 manifest와 데이터베이스를
완전히 삭제하기 전에 사용자 백업을 권장합니다.

이 C# 프로젝트는 **새 경로** `C:\ProgramData\GameBarCpuProfile`에만
자신의 manifest 및 GUID를 만듭니다. 기존 PowerShell 설치본을 임의로 삭제하지 않습니다.

## 실제 동작

1. 시작 시, 현재 로드된 일반 사용자 `HKEY_USERS\<SID>\System\GameConfigStore\Children`를 한 번 읽습니다.
2. `MatchedExeFullPath`의 EXE를 검사해 PE 헤더로 x64/x86을 구분합니다.
3. 서비스의 manifest에서 게임 목록 및 실행 파일 아키텍처 변화를 반영합니다. 파일 크기는 매칭에 사용하지 않습니다.
4. 변경된 아키텍처만 XML을 다시 생성하고 **ADK의 `fixApp.exe`**로 SDB로 컴파일합니다.
5. 컴파일이 성공한 경우에만 **Windows의 `sdbinst.exe`**로 설치합니다.
6. 다시 레지스트리 변경 이벤트 대기 상태로 돌아갑니다.

서비스 자체는 일반 게임 실행 중 지속적인 CPU 감시, FPS 계측, GPU 접근, Affinity 설정을 하지 않습니다.
기본값은 `ProcessorCountLie`이며, `settings.json`의 `ShimMode`로 `ProcessorCountLie2`를 전역 선택할 수 있습니다. 두 모드 모두 **이미 실행 중인 게임에는 재시작 후 적용**됩니다. `ProcessorCountLie2`는 Shim 동작으로 Affinity까지 제한할 수 있으며, 서비스가 직접 프로세스 Affinity를 설정하는 기능은 아닙니다.

- `x64.sdb`: 64비트 게임들을 하나의 데이터베이스에 저장
- `x86.sdb`: 32비트 게임들을 하나의 데이터베이스에 저장
- 사용자별 Game Bar 기록을 병합하며, 등록 해제 시 해당 사용자 항목만 제거
- 다른 사용자의 프로필이 **로드되지 않은** 상태는 제거 사유로 보지 않음
- 일치 조건은 **EXE 파일명**으로, 게임 파일 크기가 달라져도 매칭을 유지합니다. 다른 폴더의 동일한 이름의 EXE에까지 매칭될 수 있습니다.
- 같은 아키텍처의 등록 게임에 EXE 파일명 중복이 있으면 양쪽을 SDB에서 제외하고 `AMBIGUOUS EXE`로 기록합니다.
- `PruneRemoved` 기본 활성화. 해당 Game Bar 레지스트리 기록이 사라질 때만 삭제합니다.

## 설정 및 점검

설치·최초 실행 후 데이터 경로:

```
C:\ProgramData\GameBarCpuProfile\
  settings.json
  manifest.json
  x64.xml, x64.sdb
  x86.xml, x86.sdb
  service.log
```

`settings.json` 예시:

```json
{
  "ProcessorCount": 16,
  "ShimMode": "ProcessorCountLie",
  "PruneRemoved": true,
  "DebounceMilliseconds": 450,
  "DatabaseName": "GameBar CPU Profile",
  "FixAppPath": ""
}
```

설정 파일을 변경하면 **서비스를 재시작**하세요. 실시간 설정 다시 읽기는 아직 지원하지 않습니다. 기존 `settings.json`에 `ShimMode`가 없어도 기본 `ProcessorCountLie`로 로드되며, 이름은 `ProcessorCountLie` 또는 `ProcessorCountLie2`만 허용합니다. 모드 변경 시 XML SHA-256 해시가 달라지므로 기존 SDB GUID를 유지한 채 새 SDB를 컴파일·설치합니다.

```powershell
# 설치된 서비스 확인
sc.exe query GameBarCpuProfile

# 개발자 CLI — 관리자 PowerShell (설치 폴더에서)
.\GameBarCpuProfile.Service.exe --check-tools

# 수동 동기화 (서비스를 중지한 상태에서 권장)
.\GameBarCpuProfile.Service.exe --sync-once

# 특정 게임 제외 및 재허용
.\GameBarCpuProfile.Service.exe --exclude "D:\Games\Example.exe"
.\GameBarCpuProfile.Service.exe --include "D:\Games\Example.exe"
```

특정 게임을 제외한 뒤에는 서비스를 재시작해 다시 동기화합니다.

### ADK와 공개 배포 시 제한

`fixApp.exe`를 이용한 XML→SDB 변환은 **비공식·문서화되지 않은 인터페이스**입니다.
`Application Compatibility Tools`를 설치해도 컴파일러가 모든 배포판에 포함되는지는
반드시 `--check-tools`로 확인하세요. 이는 GitHub에 그대로 재배포할 수 있는
독립 실행형 SDB 컴파일러가 아닙니다. **Microsoft ADK 바이너리를 프로젝트에
포함하거나 재배포하지 마세요.**

ADK가 없으면 Game Bar 이벤트 감시는 가능하지만 **새 SDB를 컴파일·설치하지 못합니다.**
기존 설치된 데이터베이스는 그대로 유지하며, 재시작 또는 수동 동기화로 재시도할 수 있습니다.

### 서비스 계정 / 보안

서비스는 기본적으로 `LocalSystem` 계정으로 실행됩니다. 일반적인 서비스의 HKCU는
실제 로그인한 사용자의 HKCU가 아니므로, 서비스는 로드된 일반 사용자 SID 아래의
`HKEY_USERS` 게임 기록을 조회합니다. 다른 사용자의 기록을 병합하면 SDB가 **시스템
전체에 등록**되므로 해당 사용자에게 영향을 줄 수 있습니다.

관리자 권한이 필요한 프로그램입니다. `C:\ProgramData\GameBarCpuProfile` 폴더
내 설정과 XML은 SYSTEM/관리자만 수정할 수 있도록 ACL을 확인하세요.
임의의 게임 등록은 관리자 권한의 SDB 설치로 이어지므로, 공개 배포 전에 보안
검토와 다중 사용자 환경 테스트가 필요합니다.

안티치트가 있는 게임에서는 호환성 Shim이 차단되거나 게임 정책과 충돌할 수 있습니다.
해당 게임은 `--exclude`로 명시적으로 제외하는 것을 권장합니다.

## 제거 및 업데이트

- 설치 파일 재실행: 기존 서비스를 중지하고 새 EXE로 교체한 다음 재시작
- Windows 설정 → 설치된 앱 → **GameBar CPU Profile 제거**:
  서비스 정지 → **이 서비스가 설치한** x64/x86 SDB 제거 → 서비스 삭제 → 실행파일 제거
- 원본 데이터 폴더와 로그는 진단·복구를 위해 자동 삭제하지 않음
- 이전 PowerShell 스크립트가 설치한 데이터베이스는 손대지 않음

## 검증 체크리스트 (공개 전 필수)

- [ ] Windows 11 x64에서 `dotnet build` 에러 없이 성공
- [ ] `--check-tools`에서 fixApp.exe, sdbinst.exe 확인
- [ ] 게임 1개로 x64 SDB 컴파일, 설치 및 Compatibility Administrator에서 확인
- [ ] 기본 `ProcessorCountLie=16` 전후 게임 스레드 수 비교
- [ ] `ShimMode`를 `ProcessorCountLie2`로 변경한 뒤 x64/x86 XML의 `<SHIM NAME>`과 로그 확인
- [ ] 다시 `ProcessorCountLie`로 변경하면 XML/SDB가 원래 모드로 갱신되는지 확인
- [ ] 기존 `settings.json` (`ShimMode` 필드 없음)으로 업그레이드해도 기본 모드 유지
- [ ] `ProcessorCountLie2`가 실험 시스템에서 변경한 Affinity 및 백그라운드 유지 여부 확인
- [ ] Game Bar 게임 등록/해제 즉시 반영
- [ ] 32비트 EXE도 x86 SDB에 반영
- [ ] 서비스 종료/재시작 및 Windows 로그인·로그아웃 시 안정성
- [ ] 기존 SDB 갱신 시 GUID 유지, 설치 목록 중복 없음
- [ ] `x64.xml` / `x86.xml`의 `<EXE>` 태그에 `SIZE=` 속성이 없음
- [ ] EXE 파일 크기가 바뀌어도 같은 이름이라면 SDB 매칭이 유지됨
- [ ] 같은 아키텍처에서 동명 EXE가 중복 등록되면 `AMBIGUOUS EXE`로 제외됨
- [ ] 업그레이드 인스톨러로 서비스 EXE 교체 정상
- [ ] 제거 시 **해당 GUID의** 설치된 SDB만 제거
- [ ] 관리자 계정과 로그인 계정이 다른 UAC 환경

## 기술 참고

- [Microsoft: BackgroundService Windows Service](https://learn.microsoft.com/en-us/dotnet/core/extensions/windows-service)
- [Microsoft: Using sdbinst.exe](https://learn.microsoft.com/en-us/previous-versions/windows/it-pro/windows-10/deployment/compatibility/using-the-sdbinstexe-command-line-tool)
- [Microsoft: RegNotifyChangeKeyValue](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regnotifychangekeyvalue)
- [fixApp XML→SDB 인터페이스 분석(비공식)](https://research.nasbench.dev/research/other/finding-shimdbc)
