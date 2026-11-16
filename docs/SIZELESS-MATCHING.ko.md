# v0.1.2: 파일 크기 독립적인 EXE 매칭

기존 서비스는 SDB XML의 `<EXE>` 태그에 `SIZE` 속성을 지정했습니다. 게임이 업데이트되어 실행 파일 크기가 바뀌면 해당 SDB가 더 이상 매칭되지 않을 수 있었습니다.

v0.1.2부터는 다음처럼 `SIZE` 속성을 생성하지 않습니다.

```xml
<EXE NAME="ExampleGame.exe" ID="{...}" RUNTIME_PLATFORM="AMD64">
  <SHIM NAME="ProcessorCountLie" COMMAND_LINE="16" />
</EXE>
```

- 기존 `manifest.json`의 `Size` 필드는 역직렬화 시 무시됩니다. 기존 데이터베이스 GUID, 각 게임의 GUID, 경로, 게임별 제외 목록을 유지합니다.
- EXE 크기를 바꾸어도 더는 크기 변경 자체를 이유로 SDB를 갱신하지 않습니다.
- 새 버전 첫 동기화에서 생성 XML이 달라지므로 기존 설치된 SDB를 **동일 데이터베이스 GUID**로 다시 설치합니다.
- 동일 아키텍처에 **같은 EXE 파일명을 가진 두 개 이상의 등록 경로**가 있으면, 크기로 구분할 수 없으므로 해당 경로는 전부 SDB에서 제외합니다. 로그에 `AMBIGUOUS EXE`가 남습니다.
- SDB는 경로를 고정하는 기능이 아닙니다. **다른 폴더에 있는 같은 파일명 EXE에도 일치할 수 있으므로** 충돌 위험을 감수해야 합니다.
- AMD/Windows에서 설치한 별도 SDB에 동일 게임의 호환성 Fix가 있을 수 있습니다. 충돌 시 어떤 `ProcessorCountLie`가 유효한지는 별도로 검증해야 합니다.

## 확인 절차

1. Windows 환경에서 `.\scripts\build-installer.ps1`로 설치 프로그램을 생성합니다.
2. 기존 설치본 위에 설치한 뒤 서비스를 다시 시작합니다.
3. `C:\ProgramData\GameBarCpuProfile\x64.xml` 또는 `x86.xml`을 열어 `SIZE=`가 없는지 확인합니다.
4. Compatibility Administrator에서 설치된 관리 DB에 `ProcessorCountLie`가 정상 표시되는지 확인합니다.
5. 테스트 게임의 EXE가 업데이트된 뒤에도 동일한 이름이면 Fix가 적용되는지 확인합니다.
6. 적용/해제 테스트는 게임을 완전히 종료하고 다시 실행하여 비교합니다.
