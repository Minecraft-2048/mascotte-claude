"""Construit l'atlas d'animation de la mascotte Claude a partir de la planche generee par l'IA.

Entree : sources/planche-1.png  (12 poses sur fond vert, grille 4 x 3)
Sorties : assets/claude-atlas.png (8 colonnes x 9 lignes, cases de 192x208, meme disposition que les pets Codex)
          sources/apercu-atlas.png et sources/apercu.gif (controle visuel)

Usage : python outils/construire_atlas.py
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

RACINE = Path(__file__).resolve().parent.parent
PLANCHE = RACINE / "sources" / "planche-1.png"
ATLAS = RACINE / "assets" / "claude-atlas.png"

CASE_L, CASE_H = 192, 208      # case de l'atlas (contrat des pets Codex)
SOL = 200                      # ligne du sol (bas des pieds) dans la case
HAUTEUR = 132                  # hauteur de la pose de repos dans la case

POSES = ["repos", "clin", "salut_a", "salut_b", "marche_a", "marche_b",
         "saut", "accroupi", "rate", "attente", "travail", "revue"]

# (ligne de l'atlas, nom, durees en ms) : memes lignes et memes rythmes que les pets Codex
ETATS = [
    ("idle", [280, 110, 110, 140, 140, 320]),
    ("running-right", [120] * 7 + [220]),
    ("running-left", [120] * 7 + [220]),
    ("waving", [140, 140, 140, 280]),
    ("jumping", [140, 140, 140, 140, 280]),
    ("failed", [140] * 7 + [240]),
    ("waiting", [150] * 5 + [260]),
    ("running", [120] * 5 + [220]),
    ("review", [150] * 5 + [280]),
]


# ---------------------------------------------------------------- decoupage
def masque_fond(a):
    r, g, b = a[..., 0].astype(int), a[..., 1].astype(int), a[..., 2].astype(int)
    return (g - np.maximum(r, b)) > 60


def bandes(profil, mini=12):
    """Intervalles [debut, fin) ou le profil booleen est vrai, en ignorant les miettes."""
    res, debut = [], None
    for i, v in enumerate(list(profil) + [False]):
        if v and debut is None:
            debut = i
        elif not v and debut is not None:
            if i - debut >= mini:
                res.append((debut, i))
            debut = None
    return res


def decouper(a):
    plein = ~masque_fond(a)
    boites = []
    for y0, y1 in bandes(plein.any(axis=1)):
        for x0, x1 in bandes(plein[y0:y1].any(axis=0)):
            ys = np.where(plein[y0:y1, x0:x1].any(axis=1))[0]
            boites.append((x0, y0 + ys[0], x1, y0 + ys[-1] + 1))
    return boites


def detourer(a, boite):
    """Sprite RGBA a la resolution d'origine, fond vert retire et reflets verts neutralises."""
    x0, y0, x1, y1 = boite
    crop = a[y0:y1, x0:x1].copy()
    fond = masque_fond(crop)
    plafond = np.maximum(crop[..., 0], crop[..., 2])
    crop[..., 1] = np.minimum(crop[..., 1], plafond)      # aucune couleur du personnage n'a de vert dominant
    rgba = np.dstack([crop, np.where(fond, 0, 255).astype(np.uint8)])
    rgba[fond, :3] = 0
    return rgba


# ------------------------------------------------------------------ visage
def dilater(masque, rayon):
    im = Image.fromarray((masque * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(2 * rayon + 1))
    return np.asarray(im) > 127


def boite_visiere(s):
    """Rectangle englobant de la visiere : la tache sombre qui contient le milieu du visage.
    (Le contour du corps est sombre lui aussi, d'ou le remplissage de proche en proche.)"""
    r, g, b = (s[..., i].astype(int) for i in range(3))
    sombre = (s[..., 3] > 0) & (r < 85) & (g < 70) & (b < 70)
    h, w = sombre.shape
    y, x = int(h * 0.42), w // 2
    while not sombre[y, x]:                    # le milieu peut tomber sur un oeil
        x -= 1
    vu = np.zeros_like(sombre)
    pile = [(y, x)]
    vu[y, x] = True
    while pile:
        y, x = pile.pop()
        for yy, xx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= yy < h and 0 <= xx < w and sombre[yy, xx] and not vu[yy, xx]:
                vu[yy, xx] = True
                pile.append((yy, xx))
    ys, xs = np.where(vu)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1, np.median(s[vu][:, :3], axis=0)


def masque_yeux(s, boite):
    x0, y0, x1, y1 = boite[:4]
    r, g, b = (s[..., i].astype(int) for i in range(3))
    clair = (r > 170) & (g > 150) & (b > 110)
    dedans = np.zeros(clair.shape, bool)
    dedans[y0:y1, x0:x1] = True
    return clair & dedans


def clin_d_oeil(repos, clin):
    """Meme corps que la pose de repos, mais avec les yeux fermes de la pose 'clin' :
    le corps reste identique au pixel pres, seul le regard change."""
    br, bc = boite_visiere(repos), boite_visiere(clin)
    res = repos.copy()
    res[dilater(masque_yeux(repos, br), 4), :3] = br[4]
    dx = (br[0] + br[2]) // 2 - (bc[0] + bc[2]) // 2
    dy = (br[1] + br[3]) // 2 - (bc[1] + bc[3]) // 2
    ys, xs = np.where(dilater(masque_yeux(clin, bc), 2))
    res[ys + dy, xs + dx] = clin[ys, xs]
    return res


# ------------------------------------------------------------ pose des images
def centre_pieds(s):
    bas = s[int(s.shape[0] * 0.9):, :, 3] > 0
    cols = np.where(bas.any(axis=0))[0]
    return (cols[0] + cols[-1] + 1) / 2


class Atelier:
    def __init__(self, sprites):
        self.sprites = sprites
        self.k = HAUTEUR / sprites["repos"].shape[0]
        self.cx = {n: centre_pieds(s) for n, s in sprites.items()}

    def image(self, nom, dx=0, dy=0, e=0, m=False):
        """Une case de l'atlas. e = ecrasement (respiration, appui), m = miroir."""
        s, cx = self.sprites[nom], self.cx[nom]
        if m:
            s, cx = s[:, ::-1], s.shape[1] - cx
        kx, ky = self.k * (1 + 0.012 * e), self.k * (1 - 0.022 * e)
        l, h = max(1, round(s.shape[1] * kx)), max(1, round(s.shape[0] * ky))
        im = Image.fromarray(np.ascontiguousarray(s), "RGBA").convert("RGBa")
        im = im.resize((l, h), Image.Resampling.LANCZOS).convert("RGBA")
        x0, y0 = round(CASE_L / 2 - cx * kx) + dx, SOL - h + dy
        if x0 < 0 or y0 < 0 or x0 + l > CASE_L or y0 + h > CASE_H:
            print(f"  attention : {nom} deborde de la case ({x0},{y0} {l}x{h})")
        case = Image.new("RGBA", (CASE_L, CASE_H), (0, 0, 0, 0))
        case.alpha_composite(im, (x0, y0))
        return case


def main():
    a = np.asarray(Image.open(PLANCHE).convert("RGB"))
    boites = decouper(a)
    print(f"{len(boites)} poses trouvees")
    assert len(boites) == len(POSES), "la planche doit contenir 12 poses bien separees"
    sprites = {n: detourer(a, b) for n, b in zip(POSES, boites)}
    sprites["clin"] = clin_d_oeil(sprites["repos"], sprites["clin"])
    f = Atelier(sprites).image

    # icone de l'exe : la pose de repos, a sa resolution d'origine
    repos = Image.fromarray(sprites["repos"], "RGBA")
    cote = max(repos.size)
    icone = Image.new("RGBA", (cote, cote), (0, 0, 0, 0))
    icone.alpha_composite(repos, ((cote - repos.width) // 2, (cote - repos.height) // 2))
    ATLAS.parent.mkdir(exist_ok=True)
    icone.resize((256, 256), Image.Resampling.LANCZOS).save(
        ATLAS.parent / "icone.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

    lignes = [
        # 0 idle : respiration, clin d'oeil
        [f("repos"), f("repos", e=1), f("repos", e=2), f("repos", e=1), f("clin"), f("repos")],
        # 1 running-right : marche vers la droite
        [f("marche_a"), f("marche_a", dy=-3), f("marche_b"), f("marche_b", dy=-3)] * 2,
        # 2 running-left : la meme marche en miroir
        [f("marche_a", m=True), f("marche_a", dy=-3, m=True), f("marche_b", m=True), f("marche_b", dy=-3, m=True)] * 2,
        # 3 waving
        [f("salut_a"), f("salut_b"), f("salut_a"), f("salut_b")],
        # 4 jumping : appel, montee, sommet, descente, reception
        [f("accroupi"), f("saut", dy=-22), f("saut", dy=-46), f("saut", dy=-20), f("accroupi")],
        # 5 failed : frisson puis affaissement
        [f("rate"), f("rate", dx=-3), f("rate", dx=3), f("rate", dx=-3), f("rate", dx=3),
         f("rate", e=1), f("rate", e=3), f("rate", e=3)],
        # 6 waiting : petits rebonds pour attirer l'attention
        [f("attente"), f("attente", dy=-3), f("attente", dy=-6), f("attente", dy=-3), f("attente"), f("attente", e=1)],
        # 7 running : tape sur son ordinateur
        [f("travail"), f("travail", e=1)] * 3,
        # 8 review : se penche pour verifier
        [f("revue"), f("revue", e=1), f("revue", e=2), f("revue", e=2), f("revue", e=1), f("revue")],
    ]

    atlas = Image.new("RGBA", (CASE_L * 8, CASE_H * len(lignes)), (0, 0, 0, 0))
    for l, images in enumerate(lignes):
        assert len(images) == len(ETATS[l][1]), ETATS[l][0]
        for c, case in enumerate(images):
            atlas.paste(case, (c * CASE_L, l * CASE_H))
    ATLAS.parent.mkdir(exist_ok=True)
    atlas.save(ATLAS, optimize=True)
    print(f"atlas ecrit : {ATLAS} ({atlas.width}x{atlas.height})")

    # controle visuel : planche contact + gif de tous les etats
    teinte = (43, 45, 49, 255)
    fond = Image.new("RGBA", atlas.size, teinte)
    fond.alpha_composite(atlas)
    fond.resize((atlas.width // 2, atlas.height // 2), Image.Resampling.LANCZOS).save(RACINE / "sources" / "apercu-atlas.png")
    vues, durees = [], []
    for images, (_, ms) in zip(lignes, ETATS):
        for _ in range(2):
            for case, d in zip(images, ms):
                v = Image.new("RGBA", (CASE_L, CASE_H), teinte)
                v.alpha_composite(case)
                vues.append(v.convert("P", palette=Image.Palette.ADAPTIVE))
                durees.append(d)
    vues[0].save(RACINE / "sources" / "apercu.gif", save_all=True, append_images=vues[1:], duration=durees, loop=0)


if __name__ == "__main__":
    main()
