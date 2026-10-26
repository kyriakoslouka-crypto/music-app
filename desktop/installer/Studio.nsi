Unicode true
!include "MUI2.nsh"
!ifndef APP_ID
 !define APP_ID "ProfessionalEarTrainingStudio"
!endif
!ifndef APP_NAME
 !define APP_NAME "Professional Ear Training Studio"
!endif
!ifndef APP_VERSION
  !define APP_VERSION "0.2.0"
!endif
!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\artifacts\win-x64"
!endif
!ifndef OUTPUT_FILE
  !define OUTPUT_FILE "..\artifacts\${APP_ID}-${APP_VERSION}-Setup.exe"
!endif
!ifndef UNINSTALL_INCLUDE
  !define UNINSTALL_INCLUDE "..\artifacts\uninstall-files.nsh"
!endif
Name "${APP_NAME} ${APP_VERSION}"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\${APP_ID}"
RequestExecutionLevel user
SetCompressor /SOLID lzma
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Greek"
!insertmacro MUI_LANGUAGE "English"
Section "${APP_NAME}" SEC_MAIN
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_ID}.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}" "DisplayName" "${APP_NAME} ${APP_VERSION} (MVP)"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}" "NoRepair" 1
SectionEnd
Section "Uninstall"
  SetShellVarContext current
  ; Generated from the exact publish manifest; keep user-added files and progress.
  !include "${UNINSTALL_INCLUDE}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}"
SectionEnd
