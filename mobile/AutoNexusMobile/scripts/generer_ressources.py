"""Régénère les polices de l'app mobile à partir de celles du front web.

- Inter (texte et titres), la police du web (client/src/index.css), paquet @fontsource/inter,
  sous-ensemble latin, convertie en TTF.
- Icônes Lucide, la famille du web (lucide-react), police du paquet lucide-static à la même
  version, réduite aux glyphes de Resources/Icons.cs.

L'icône de l'app et l'écran de démarrage sont des SVG (Resources/AppIcon, Resources/Splash)
repris du LogoIcon du web : rien à générer pour eux.

Dépendance : fonttools (pip install fonttools).

Usage, depuis la racine du dépôt (versions : celles de client/package.json) :
    npm pack lucide-static@1.14.0 @fontsource/inter@5.2.8
    mkdir lucide inter && tar -xzf lucide-static-1.14.0.tgz -C lucide && tar -xzf fontsource-inter-5.2.8.tgz -C inter
    python mobile/AutoNexusMobile/scripts/generer_ressources.py --lucide lucide/package --inter inter/package
"""

import argparse
import re
from pathlib import Path

from fontTools.subset import Options, Subsetter
from fontTools.ttLib import TTFont

MOBILE = Path(__file__).resolve().parents[1]
FONTS = MOBILE / "Resources/Fonts"

# (graisse, nom du TTF)
INTER = [(400, "Inter-Regular"), (500, "Inter-Medium"), (600, "Inter-SemiBold"), (700, "Inter-Bold")]


def generate_inter(package: Path) -> None:
    for weight, name in INTER:
        font = TTFont(package / "files" / f"inter-latin-{weight}-normal.woff")
        font.flavor = None
        font.save(FONTS / f"{name}.ttf")


def generate_lucide(package: Path) -> None:
    icons = (MOBILE / "Resources/Icons.cs").read_text(encoding="utf-8")
    codepoints = [int(code, 16) for code in re.findall(r'"\\u([0-9a-fA-F]{4})"', icons)]
    font = TTFont(package / "font" / "lucide.ttf")
    options = Options()
    options.hinting = False
    options.drop_tables += ["GSUB", "GPOS"]
    subsetter = Subsetter(options)
    subsetter.populate(unicodes=codepoints)
    subsetter.subset(font)
    font.save(FONTS / "Lucide.ttf")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--lucide", type=Path, required=True, help="dossier « package » extrait de lucide-static")
    parser.add_argument("--inter", type=Path, required=True, help="dossier « package » extrait de @fontsource/inter")
    args = parser.parse_args()

    FONTS.mkdir(parents=True, exist_ok=True)
    generate_inter(args.inter)
    generate_lucide(args.lucide)


if __name__ == "__main__":
    main()
