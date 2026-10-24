Unicode true
!include "MUI2.nsh"
!ifndef APP_VERSION
  !define APP_VERSION "0.2.0"
!endif
!ifndef PUBLISH_DIR
  !define PUBLISH_DIR "..\artifacts\win-x64"
!endif
!ifndef OUTPUT_FILE
  !define OUTPUT_FILE "..\artifacts\ProfessionalEarTrainingStudio-${APP_VERSION}-Setup.exe"
!endif
!ifndef UNINSTALL_INCLUDE
  !define UNINSTALL_INCLUDE "..\artifacts\uninstall-files.nsh"
!endif
Name "Professional Ear Training Studio ${APP_VERSION}"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\ProfessionalEarTrainingStudio"
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
Section "Professional Ear Training Studio" SEC_MAIN
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\Professional Ear Training Studio"
  CreateShortcut "$SMPROGRAMS\Professional Ear Training Studio\Professional Ear Training Studio.lnk" "$INSTDIR\ProfessionalEarTrainingStudio.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ProfessionalEarTrainingStudio" "DisplayName" "Professional Ear Training Studio ${APP_VERSION} (MVP)"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ProfessionalEarTrainingStudio" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ProfessionalEarTrainingStudio" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ProfessionalEarTrainingStudio" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ProfessionalEarTrainingStudio" "NoRepair" 1
SectionEnd
Section "Uninstall"
  SetShellVarContext current
  ; Generated from the exact publish manifest; keep user-added files and progress.
  !include "${UNINSTALL_INCLUDE}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\Professional Ear Training Studio\Professional Ear Training Studio.lnk"
  RMDir "$SMPROGRAMS\Professional Ear Training Studio"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\ProfessionalEarTrainingStudio"
SectionEnd
