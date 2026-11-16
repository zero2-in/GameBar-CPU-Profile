# v0.1.1 Mutex 해제 오류 수정

## 원인

이전 `DatabaseSynchronizer.SyncAsync()`는 `Mutex.WaitOne()` 이후 `await SyncArchAsync(...)`를 호출하고 `finally`에서 `Mutex.ReleaseMutex()`를 실행했습니다. .NET의 named Mutex는 **작업(Task)이 아니라 실행 스레드에 소유권이 귀속**되므로, `await` 후 다른 스레드에서 재개되면 `System.ApplicationException`이 발생할 수 있습니다.

## 수정

`SyncAsync()`는 `Task.Run()`을 통해 동기화 루틴 전체를 단일 thread-pool 스레드에서 수행합니다. 이 스레드가 named Mutex를 획득하고, `SyncArchAsync(...).GetAwaiter().GetResult()`로 비동기 작업이 완료되기를 기다린 후, **그 동일한 스레드**에서 `ReleaseMutex()`를 수행합니다. 백그라운드 서비스에는 UI 동기화 컨텍스트가 없고, 게임 등록 변경 시에만 발생하는 작업이라 이 접근의 추가적인 스레드 점유 비용은 제한적입니다.

`Mutex`의 전역 이름 및 기존 데이터베이스 GUID, x64/x86 SDB 경로, 매니페스트는 변경하지 않습니다.

## 설치

1. 기존 서비스가 실행 중이면 인스톨러가 중지하고 교체하도록 설계되어 있습니다.
2. VS Code에서 `./scripts/build-installer.ps1`로 `GameBarCpuProfile-Setup-0.1.1-x64.exe`를 빌드합니다.
3. 관리자 권한으로 인스톨러를 실행합니다.
4. Game Bar에서 테스트 게임을 등록/해제하여 `C:\ProgramData\GameBarCpuProfile\service.log`를 확인합니다.

**중요:** 여기서 C# 소스만 수정했으며 Windows에서의 컴파일과 실제 설치 검증은 필요합니다. 기존 SDB를 수동으로 삭제하지 마세요.
