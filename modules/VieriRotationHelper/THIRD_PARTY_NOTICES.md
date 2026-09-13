# Third-party notices

## Hilda display reference

The overlay renderer, keyboard-label formatting, positional glyph selection,
and bundled Miedinger font asset reference Hilda 7.5.1.0 under the owner's
permission supplied for this project. Hilda assembly and font hashes are
recorded in `upstream/hilda.lock.json`. Original third-party rights remain with
their respective owners; the project's MIT license does not relicense those
assets. No Hilda assembly is redistributed or required.

## Wrath Combo

VieriRotationHelper's embedded rotation rules and compatibility metadata are
derived from Wrath Combo, pinned in `upstream/wrath.lock.json`.

Wrath Combo is licensed under the BSD 3-Clause License:

Copyright (c) 2024, Puni.sh

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice,
   this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.
3. Neither the name of the copyright holder nor the names of its contributors
   may be used to endorse or promote products derived from this software
   without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.

## Bundled helper libraries

The isolated decision engine includes ECommons (MIT, Copyright 2023
NightmareXIV) and PunishLib (BSD 3-Clause, Copyright 2023 Puni.sh). Their full
licenses are included under `Engine/Upstream/ECommons/LICENSE.md` and
`Engine/Upstream/PunishLib/LICENCE.md` in the corresponding source, and in the
runtime package's `licenses` directory. Exact revisions are recorded in
`upstream/engine.lock.json`. Assembly names are isolated to this plugin.

WrathCombo.API 0.5.7 is redistributed as the compatibility contract used by
the embedded provider and automation clients. The package is licensed
AGPL-3.0-only and its source is available from
https://github.com/PunishXIV/WrathCombo.API. VieriRotationHelper registers the
same public IPC names so existing clients do not need a private Vieri-specific
integration.
