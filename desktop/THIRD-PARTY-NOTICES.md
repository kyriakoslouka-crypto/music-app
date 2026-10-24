# Third-party components

- MeltySynth 2.4.1 — MIT, Nobuaki Tanaka / contributors. https://github.com/sinshu/meltysynth . Optional user-supplied SF2 playback.
- NAudio 2.2.1 — MIT, Mark Heath / contributors. https://github.com/naudio/NAudio . Windows WASAPI/ASIO and Windows Media Foundation FLAC decoding.
- Microsoft .NET 10.0.13 runtime / Windows Desktop runtime — https://github.com/dotnet/runtime/blob/main/LICENSE.TXT , https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT , https://github.com/dotnet/winforms/blob/main/LICENSE.TXT . Runtime included in self-contained builds.
- NSIS 3.11 — compiler/installer component licenses: https://nsis.sourceforge.io/License .

## Piano library, not bundled

Maestro Concert Grand, Mats Helgesson (2003), Yamaha CF-3. SFZ/FLAC conversion by kinwie, published with author permission according to the upstream README. Source pinned to `717078b450eccc4a7733755f4b5973426ebc23e9` at https://github.com/sfzinstruments/MatsHelgesson.MaestroConcertGrandPiano .

This is **not** a permissively licensed or CC sound set. The original `mcgreadme.txt` restricts selling and modifying/redistributing the soundfont without permission. No samples or archive are included in the application's Setup/ZIP. The accessible first-run wizard downloads the original from the publisher and prepares a private local playback cache. Original copyright/legal text and README are preserved in that cache. Do not redistribute that cache. No additional publication permission has been obtained or implied. See `docs/PIANO-SOURCE.md`.

The renderer implements a dedicated local adapter, not the complete SFZ/ARIA opcode specification. Decoding the original stereo 44.1kHz/16-bit FLAC into PCM16 WAV is lossless. A Linux development test used the same preparer with ffmpeg; The Windows check uses the actual Media Foundation decoder and independently reproduced PCM SHA-256 reference fingerprints; consult windows-piano-check.json for the actual run outcome. Embedded fingerprints are metadata, not redistributed audio.

An external Florestan subset SF2 from TinySoundFont examples was also used as private integration input. It is not bundled or represented as a professional piano. No NVDA controller DLL is bundled.

The complete software licence texts are in `SOFTWARE-LICENSES.txt` in the folder/installer distribution and embedded as `Studio.Windows.ThirdPartyLicenses.txt` in the application assembly, including the standalone EXE.
