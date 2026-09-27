# Third-party notices

## POL client encryption

The UO encryption profiles and algorithms in `src/Moongate.Network.Packets/Data/Encryption/`,
`Types/Encryption/` and `Encryption/` are adapted from the POL Project's
`pol-core/pol/crypt/` implementation: https://github.com/polserver/polserver.
The test vector generator builds the original POL sources supplied by the developer;
`pol-vectors.json` records the reference commit.

Original cryptography implementation: Copyright (C) 1999-2000 Bruno 'Beosil' Heidelberger.
POL adaptation and encryption keys: TJ Houston (Myrathi) and POL contributors.

The original cryptography module grants permission under the GNU General Public License,
version 2 or (at your option) any later version. It is provided WITHOUT ANY WARRANTY,
including implied warranties of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
See https://www.gnu.org/licenses/old-licenses/gpl-2.0.html for the original license text.
This project is distributed under the GNU Affero General Public License version 3; see LICENSE.
