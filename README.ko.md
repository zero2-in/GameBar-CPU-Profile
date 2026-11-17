# GameBar CPU Profile

**Xbox Game Bar에 등록한 게임에 CPU 프로세서 수 호환성 설정을 자동 적용하는 Windows 서비스입니다.**

GameBar CPU Profile은 Windows의 **`ProcessorCountLie` / `ProcessorCountLie2`** 애플리케이션 호환성 기능을 선택적으로 사용해, 지원되는 CPU 정보 조회 API에서 게임에 보고되는 **논리 프로세서 수**를 조절합니다. 시스템 전체의 CCD를 비활성화하거나 BIOS를 변경할 필요가 없습니다.

듀얼 CCD Ryzen X3D 환경을 염두에 두고 개발했지만 AMD 전용 프로그램은 아닙니다.

> [!IMPORTANT]
> CPU 토폴로지를 완전히 가상화하지는 않습니다. 기본 `ProcessorCountLie` 모드는 실행 코어 제한을 목적으로 하지 않지만, 선택형 `ProcessorCountLie2`는 환경에 따라 프로세스 Affinity를 제한할 수 있습니다. 어느 모드도 물리적으로 CCD1을 비활성화하거나 FPS 향상을 보장하지 않습니다. 효과는 게임 엔진의 CPU 정보 조회·스레드 관리 방식에 따라 달라집니다.

## CPU Affinity / CPU Sets와 무엇이 다른가요?

| 구분 | CPU Affinity / CPU Sets | GameBar CPU Profile |
|---|---|---|
| 제어 대상 | 스레드가 실행될 CPU | 일부 CPU 정보 조회 API가 보고하는 논리 프로세서 수 |
| 게임 엔진의 워커 구성에 영향 | 엔진 구현에 따라 다름 | 대상 API를 사용하면 영향 가능 |
| 실제 코어 비활성화 | 아니요 | 아니요 |
| Affinity 변경 가능성 | Affinity는 수정, CPU Sets는 별도 제어 | **기본 모드에서는 변경 미관찰**, 선택형 `ProcessorCountLie2`는 Affinity 제한 가능 |
| 재부팅 | 불필요 | 불필요, 게임 재실행 필요 |

예를 들어 Ryzen 9 7950X3D에서 Windows는 계속 16코어 32스레드를 사용하는 한편, 호환되는 게임에는 **16개 논리 프로세서**로 보고하도록 시도할 수 있습니다.

## 주요 기능

- Xbox Game Bar에서 **게임으로 기억** 등록·해제 시 자동 동기화
- 레지스트리 변경 이벤트 기반 감시: 실행 중인 게임이나 CPU 점유율을 반복 검색하지 않음
- 64비트 게임과 32비트 게임을 **각각 통합 SDB 하나**로 관리
- 등록 변경 시 필요한 데이터베이스만 재생성·설치하고, 해제된 게임의 Fix는 자동 제거
- 기본 `ProcessorCountLie=16`, 설정 파일에서 전체 게임 대상 `ProcessorCountLie2` 모드 선택 가능
- 개별 게임 제외 및 재허용 기능

## 설치 조건

- Windows 11 x64 및 관리자 권한
- Microsoft Windows ADK의 **Application Compatibility Tools** (`fixApp.exe`가 포함된 구성)
- Xbox Game Bar
- .NET 실행 환경: 서비스 EXE에 자체 포함되어 **별도 .NET 런타임 설치 불필요**

> [!NOTE]
> SDB 빌드에 사용하는 `fixApp.exe` 명령줄 방식은 비공식·미문서화 인터페이스입니다. ADK 버전에 따라 동작이 달라질 수 있으며, 설치 프로그램은 ADK를 자동 설치하지 않습니다. AMD 또는 Microsoft의 공식 소프트웨어가 아닙니다.

## 설치 및 사용

1. [Windows ADK](https://learn.microsoft.com/ko-kr/windows-hardware/get-started/adk-install)를 설치하고 **Application Compatibility Tools**를 선택합니다.
2. GitHub **Releases** 페이지에서 `GameBarCpuProfile-Setup-…-x64.exe`를 내려받아 관리자 권한으로 설치합니다.
3. 게임을 실행한 뒤 **Win + G → 이 게임으로 기억**에 등록합니다.
4. 서비스가 해당 EXE의 호환성 Fix를 자동으로 등록합니다. **게임을 완전히 종료하고 다시 실행하면** 적용됩니다.
5. 등록을 해제하면 Game Bar 레지스트리 변경에 맞춰 해당 Fix도 자동으로 제거됩니다.

`services.msc`에서 **GameBar CPU Profile Service**를 확인할 수 있습니다.

### 등록 결과 확인

ADK의 **Compatibility Administrator (64-bit / 32-bit)**에서 해당 SDB를 열어 등록된 애플리케이션과 `ProcessorCountLie` 설정을 확인할 수 있습니다.

```text
C:\ProgramData\GameBarCpuProfile\x64.sdb
C:\ProgramData\GameBarCpuProfile\x86.sdb
```

게임에 보고되는 CPU 수는 시스템 전체 CPU 수를 변경하는 것이 아니므로, 작업 관리자 CPU 개수는 그대로 표시될 수 있습니다.

## 설정

관리자 권한으로 다음 파일을 열면 됩니다.

```text
C:\ProgramData\GameBarCpuProfile\settings.json
```

기본 설정:

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

`"ShimMode": "ProcessorCountLie2"`로 바꾸면 등록된 모든 게임(x64/x86)에 `ProcessorCountLie2`를 생성합니다. 기존 모드로 돌아오려면 `"ProcessorCountLie"`로 설정하세요. `ShimMode`가 없는 **이전 버전의 설정 파일은 기본 모드(`ProcessorCountLie`)로 호환**되므로, 기존 사용자는 해당 키를 직접 추가하면 됩니다.

설정 변경 뒤에는 관리자 PowerShell에서 서비스를 다시 시작하세요. 서비스가 기존 SDB의 GUID를 유지하면서 새 설정으로 다시 빌드·설치합니다. **게임은 반드시 완전히 종료한 후 다시 실행**해야 합니다. 이 옵션은 개별 게임이 아닌 **전체 게임 일괄 적용**입니다.

> [!WARNING]
> `ProcessorCountLie2`는 일부 환경에서 CPU Affinity까지 변경되는 것이 관찰되었습니다. 게임을 백그라운드로 전환해도 해당 Affinity 제한이 유지될 수 있으며, 논리 프로세서 번호가 CCD와 대응하는 방식은 시스템에 따라 다릅니다. V-Cache CCD를 자동 선택하는 기능이 아니며, 일부 게임에서는 성능이 악화될 수 있습니다. 필요한 게임은 `--exclude`로 제외하세요.

```powershell
Restart-Service GameBarCpuProfile
```

게임을 제외하거나 다시 포함하려면 관리자 터미널에서 다음과 같이 실행합니다.

```powershell
& "$env:ProgramFiles\GameBar CPU Profile\GameBarCpuProfile.Service.exe" --exclude "D:\Games\Example.exe"
Restart-Service GameBarCpuProfile

# 다시 포함
& "$env:ProgramFiles\GameBar CPU Profile\GameBarCpuProfile.Service.exe" --include "D:\Games\Example.exe"
Restart-Service GameBarCpuProfile
```

문제가 생기면 다음 로그를 확인하세요.

```text
C:\ProgramData\GameBarCpuProfile\service.log
```

## 제거

Windows **설정 → 앱 → 설치된 앱 → GameBar CPU Profile → 제거**에서 삭제할 수 있습니다. 프로그램이 설치한 SDB와 서비스가 제거되며, 진단·복구를 위해 일부 데이터와 로그는 남을 수 있습니다.

## 제한 및 주의사항

- Shim이 가로채는 API를 사용하지 않는 게임에는 효과가 없을 수 있으며, 전체 스레드 수 감소는 보장되지 않습니다.
- 물리 코어 수·캐시·CPUID 등을 포함한 모든 CPU 토폴로지가 가상화되지는 않습니다.
- CCD 식별·선택이나 코어 파킹 강제 기능은 없습니다. `ProcessorCountLie2`로 Affinity가 제한될 가능성과 CCD 자동 선택은 별개입니다.
- 안티치트가 있는 게임은 호환성 Shim이 차단되거나 이용 정책과 충돌할 수 있으므로 적용 대상에서 제외하는 것이 좋습니다.
- SDB 등록은 **시스템 전체**에 적용됩니다. **EXE 파일명만으로 매칭**하며 파일 크기는 검사하지 않으므로 게임 업데이트로 크기가 바뀌어도 매칭이 유지됩니다. 그러나 다른 경로의 동일한 파일명을 가진 EXE에도 적용될 수 있습니다. 같은 아키텍처에서 동일한 파일명의 게임 2개가 등록되면 둘 다 SDB에서 제외하고 `AMBIGUOUS EXE`로 기록합니다.
- AMD/Windows에서 제공한 별도 호환성 데이터베이스에 일부 게임의 `ProcessorCountLie`가 이미 지정되어 있을 수 있습니다. 같은 게임에 서로 다른 값을 설정한 경우 적용 우선순위는 가정할 수 없으므로 실제 적용 여부를 확인하거나 `--exclude`로 이 프로그램의 Fix를 제외하세요.
- 호환성 설정 변경 후 실행 중인 게임은 **재실행**해야 합니다.
- Windows 및 ADK 업데이트로 내부 API·등록 방식이 변경될 수 있습니다.

## 개발자 정보

C# Windows 서비스, Inno Setup 설치 관리자, PowerShell 빌드 스크립트를 제공하며, 자세한 빌드 및 테스트 절차는 [개발자 문서](docs/DEVELOPMENT.ko.md)를 참고하세요.

MIT 라이선스에 따라 배포됩니다. 이슈 및 기여를 환영합니다.
