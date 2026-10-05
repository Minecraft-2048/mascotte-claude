// Stickman : un bonhomme-bâton qui vit sur le bureau, dans l'esprit des stick figures d'Alan Becker.
// Contrairement aux autres mascottes (images toutes faites), il est dessiné par le programme à partir
// d'un squelette : c'est ce qui permet de choisir sa couleur et d'avoir des centaines d'animations
// (voir StickmanAnimations.cs). Compilation : construire.ps1.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MascotteStickman
{
    static class Programme
    {
        public static readonly string Dossier = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MascotteStickman");

        [STAThread]
        static int Main(string[] args)
        {
            Directory.CreateDirectory(Dossier);
            // MascotteStickman.exe --jouer "Salto avant" : fait jouer une animation à la mascotte déjà lancée
            if (args.Length >= 2 && args[0] == "--jouer")
            {
                File.WriteAllText(Path.Combine(Dossier, "commande.txt"), string.Join(" ", args, 1, args.Length - 1));
                return 0;
            }
            Biblio.Construire();
            R.Familles();
            // MascotteStickman.exe --planches dossier [filtre] : planches de contrôle des animations, en PNG
            if (args.Length >= 2 && args[0] == "--planches")
            {
                Planches.Ecrire(args[1], args.Length > 2 ? args[2] : "");
                return 0;
            }
            if (args.Length >= 2 && args[0] == "--sons") { Sons.Exporter(args[1]); return 0; }

            bool premiere;
            using (new Mutex(true, "MascotteStickman-Instance", out premiere))
            {
                if (!premiere) return 0;
                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                app.DispatcherUnhandledException += (s, e) =>
                {
                    try { File.AppendAllText(Path.Combine(Dossier, "erreurs.log"), DateTime.Now + " " + e.Exception + "\r\n"); }
                    catch (IOException) { }
                    e.Handled = true;
                };
                app.Run(new Bonhomme());
            }
            return 0;
        }
    }

    // ============================================================ réglages

    sealed class Param
    {
        public string Cle, Nom, Cat;
        public char Type;                        // b = case à cocher, n = nombre, c = choix, k = couleur
        public double V, Defaut, Min, Max;
        public string[] Choix;
    }

    static class R
    {
        public static readonly List<Param> Tous = new List<Param>();
        public static readonly HashSet<string> Coupees = new HashSet<string>();     // animations décochées
        public static bool Sale;
        static readonly Dictionary<string, Param> index = new Dictionary<string, Param>();
        static readonly Dictionary<string, double> lus = new Dictionary<string, double>();
        static string Fichier { get { return Path.Combine(Programme.Dossier, "reglages.txt"); } }

        static R()
        {
            if (File.Exists(Fichier))
                foreach (string ligne in File.ReadAllLines(Fichier))
                {
                    int egal = ligne.IndexOf('=');
                    if (egal <= 0) continue;
                    string cle = ligne.Substring(0, egal), valeur = ligne.Substring(egal + 1);
                    double nombre;
                    if (cle == "sans") Coupees.Add(valeur);
                    else if (double.TryParse(valeur, NumberStyles.Float, CultureInfo.InvariantCulture, out nombre)) lus[cle] = nombre;
                }

            const string A = "Apparence", M = "Mouvement", C = "Comportement", S = "Souris", P = "Physique", Y = "Système";
            K(A, "couleur", "Couleur", 0xFF8A1A);
            B(A, "arcenciel", "Arc-en-ciel (change de couleur en continu)", false);
            N(A, "arcVitesse", "Vitesse de l'arc-en-ciel", 60, 5, 300);
            N(A, "taille", "Taille", 1, 0.4, 3);
            N(A, "epaisseur", "Épaisseur du trait", 6, 2, 16);
            N(A, "tete", "Taille de la tête", 13, 6, 26);
            Ch(A, "styleTete", "Tête", 0, "selon la couleur, comme dans la série", "toujours pleine", "toujours creuse (anneau)");
            N(A, "torse", "Longueur du torse", 38, 18, 70);
            N(A, "bras", "Longueur des bras", 19, 8, 36);
            N(A, "jambes", "Longueur des jambes", 22, 10, 40);
            N(A, "contour", "Épaisseur du contour", 1.5, 0, 6);
            K(A, "contourCouleur", "Couleur du contour", 0x1A1A1A);
            N(A, "opacite", "Opacité (%)", 100, 15, 100);
            B(A, "ombre", "Ombre au sol", true);
            B(A, "lueur", "Halo lumineux", false);
            B(A, "trainee", "Traînée derrière la main", false);
            N(A, "traineeLongueur", "Longueur de la traînée", 14, 3, 60);
            B(A, "objets", "Accessoires (épée, ballon, notes…)", true);
            B(A, "bulles", "Bulles de texte", true);

            N(M, "vitesse", "Vitesse des animations (%)", 100, 20, 300);
            N(M, "marche", "Vitesse de déplacement (%)", 100, 20, 400);
            N(M, "fondu", "Douceur des transitions (ms)", 220, 0, 800);
            N(M, "respiration", "Respiration au repos (%)", 100, 0, 300);
            N(M, "fluidite", "Images par seconde", 60, 15, 60);
            N(M, "saut", "Puissance des sauts (%)", 100, 40, 250);

            N(C, "activite", "Secondes entre deux actions", 5, 0.5, 60);
            N(C, "tours", "Durée des animations (%)", 100, 30, 400);
            B(C, "balade", "Se promène", true);
            N(C, "distance", "Distance des promenades", 450, 60, 2500);
            B(C, "partout", "Explore tout l'écran (sinon reste près de chez lui)", true);
            B(C, "fenetres", "Saute sur le haut des fenêtres", true);
            N(C, "fenetresChance", "Envie de sauter sur une fenêtre (%)", 15, 0, 100);
            N(C, "enchaine", "Gestes pendant la marche (%)", 25, 0, 100);
            N(C, "sommeil", "S'endort après (minutes sans toucher au PC, 0 = jamais)", 10, 0, 120);
            N(C, "teleporte", "Envie de se téléporter (%)", 6, 0, 100);
            Ch(C, "styleSaut", "Sauts vers les fenêtres", 0, "variés", "simples", "toujours en salto", "atterrissage de héros");

            const string Z = "Sons";
            B(Z, "sons", "Sons activés", true);
            N(Z, "volume", "Volume (%)", 40, 0, 100);
            B(Z, "sonsSauts", "Sauts, atterrissages, rebonds et chutes", true);
            B(Z, "sonsSouris", "Quand on l'attrape, le lance ou le bouscule", true);
            B(Z, "sonsAnimations", "Bruitages des animations (coups, épée, énergie, saltos…)", true);
            B(Z, "sonsPouvoirs", "Téléportation", true);

            B(S, "regarde", "Suit le curseur du regard", true);
            B(S, "combat", "Se bat avec le curseur quand il s'approche", true);
            N(S, "portee", "Distance de combat", 130, 40, 500);
            N(S, "combatRepos", "Pause entre deux coups (s)", 2, 0.2, 20);
            B(S, "suit", "Suit le curseur", false);
            B(S, "fuit", "Fuit le curseur", false);
            B(S, "bouscule", "Se fait renverser par un curseur rapide", true);
            B(S, "attrape", "On peut l'attraper à la souris", true);
            B(S, "lancer", "On peut le lancer", true);
            Ch(S, "clic", "Un clic le fait", 4, "saluer", "sauter", "danser", "se battre", "une animation au hasard");

            N(P, "gravite", "Gravité (%)", 100, 10, 400);
            N(P, "rebond", "Rebond (%)", 40, 0, 95);
            N(P, "frottement", "Frottement au sol (%)", 40, 0, 100);
            N(P, "balancier", "Balancement quand on le porte (%)", 100, 0, 300);
            N(P, "force", "Force du lancer (%)", 100, 10, 300);
            N(P, "tournoie", "Vrilles en l'air (%)", 100, 0, 400);
            B(P, "murs", "Rebondit sur les bords de l'écran", true);

            B(Y, "premierplan", "Toujours au premier plan", true);
            N("", "droite", "", 1120, -100000, 100000);
        }

        static Param Ajouter(string cat, string cle, string nom, char type, double defaut, double min, double max)
        {
            var p = new Param { Cat = cat, Cle = cle, Nom = nom, Type = type, Defaut = defaut, Min = min, Max = max };
            double lu;
            p.V = lus.TryGetValue(cle, out lu) ? Math.Max(min, Math.Min(max, lu)) : defaut;
            Tous.Add(p);
            index[cle] = p;
            return p;
        }

        static void N(string cat, string cle, string nom, double defaut, double min, double max) { Ajouter(cat, cle, nom, 'n', defaut, min, max); }
        static void B(string cat, string cle, string nom, bool defaut) { Ajouter(cat, cle, nom, 'b', defaut ? 1 : 0, 0, 1); }
        static void K(string cat, string cle, string nom, int rvb) { Ajouter(cat, cle, nom, 'k', rvb, 0, 0xFFFFFF); }
        static void Ch(string cat, string cle, string nom, int defaut, params string[] choix) { Ajouter(cat, cle, nom, 'c', defaut, 0, choix.Length - 1).Choix = choix; }

        // Une fois les animations construites : un curseur de fréquence par famille.
        public static void Familles()
        {
            foreach (string famille in Biblio.Familles)
                N("Familles", "poids." + famille, famille, famille == "Danses" || famille == "Combat" ? 30 : 50, 0, 100);
        }

        public static double D(string cle) { return index[cle].V; }
        public static bool O(string cle) { return index[cle].V != 0; }
        public static Color Couleur(string cle) { return Rvb((int)index[cle].V); }
        public static Color Rvb(int v) { return Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v); }
        public static void Mettre(string cle, double valeur) { index[cle].V = valeur; Sale = true; }

        public static void Enregistrer()
        {
            Sale = false;
            var lignes = new List<string>();
            foreach (Param p in Tous) lignes.Add(p.Cle + "=" + p.V.ToString("0.###", CultureInfo.InvariantCulture));
            foreach (string nom in Coupees) lignes.Add("sans=" + nom);
            try { File.WriteAllLines(Fichier, lignes); }
            catch (IOException) { }
        }
    }

    // ============================================================ squelette et dessin

    static class Dessin
    {
        public struct Os
        {
            public Point Hanche, Cou, Tete, C1, M1, C2, M2, G1, P1, G2, P2;
            public double Rayon;
        }

        static Vector Bas(double degres) { double r = degres * Math.PI / 180; return new Vector(Math.Sin(r), Math.Cos(r)); }
        static Vector Haut(double degres) { double r = degres * Math.PI / 180; return new Vector(Math.Sin(r), -Math.Cos(r)); }

        // Positions des articulations, en pixels à taille 1, personnage tourné vers la droite, y vers le bas.
        // Posé : le point le plus bas touche le sol (y = 0), quelle que soit la pose — poirier, roue, allongé…
        // Suspendu : c'est la tête qui est à l'origine (tenu par le curseur).
        public static Os Calculer(double[] p, bool suspendu)
        {
            double torse = R.D("torse"), bras = R.D("bras"), jambes = R.D("jambes"), tete = R.D("tete");
            double rot = p[I.Rot], dos = rot + p[I.Torse];
            var o = new Os { Rayon = tete };
            o.Cou = o.Hanche + Haut(dos) * torse;
            o.Tete = o.Cou + Haut(dos + p[I.Tete]) * (tete + 1);
            double a = p[I.Ep1] - rot; o.C1 = o.Cou + Bas(a) * bras; o.M1 = o.C1 + Bas(a + p[I.Co1]) * bras;
            a = p[I.Ep2] - rot; o.C2 = o.Cou + Bas(a) * bras; o.M2 = o.C2 + Bas(a + p[I.Co2]) * bras;
            a = p[I.Ha1] - rot; o.G1 = o.Hanche + Bas(a) * jambes; o.P1 = o.G1 + Bas(a + p[I.Ge1]) * jambes;
            a = p[I.Ha2] - rot; o.G2 = o.Hanche + Bas(a) * jambes; o.P2 = o.G2 + Bas(a + p[I.Ge2]) * jambes;

            Vector d;
            if (suspendu) d = new Vector(-o.Tete.X, -o.Tete.Y);
            else
            {
                double bas = Math.Max(o.Tete.Y + tete, 0);
                foreach (Point point in new[] { o.Cou, o.C1, o.M1, o.C2, o.M2, o.G1, o.P1, o.G2, o.P2 }) bas = Math.Max(bas, point.Y);
                d = new Vector(p[I.X], -bas - p[I.Air]);
            }
            o.Hanche += d; o.Cou += d; o.Tete += d; o.C1 += d; o.M1 += d; o.C2 += d; o.M2 += d; o.G1 += d; o.P1 += d; o.G2 += d; o.P2 += d;
            return o;
        }

        public static Point[] Points(Os o) { return new[] { o.Hanche, o.Cou, o.Tete, o.C1, o.M1, o.C2, o.M2, o.G1, o.P1, o.G2, o.P2 }; }

        public static Pen Plume(Color couleur, double epaisseur)
        {
            return new Pen(new SolidColorBrush(couleur), epaisseur) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        }

        // Tout le corps d'abord en contour épais, puis en couleur : le contour entoure la silhouette entière.
        public static void Tracer(DrawingContext dc, Os o, Func<Point, Point> e, double s, Pen contour, Pen trait, Brush plein)
        {
            for (int passe = contour == null ? 1 : 0; passe < 2; passe++)
            {
                Pen plume = passe == 0 ? contour : trait;
                dc.DrawLine(plume, e(o.Cou), e(o.C2)); dc.DrawLine(plume, e(o.C2), e(o.M2));
                dc.DrawLine(plume, e(o.Hanche), e(o.G2)); dc.DrawLine(plume, e(o.G2), e(o.P2));
                dc.DrawLine(plume, e(o.Hanche), e(o.Cou));
                dc.DrawEllipse(passe == 1 ? plein : null, plume, e(o.Tete), o.Rayon * s, o.Rayon * s);
                dc.DrawLine(plume, e(o.Cou), e(o.C1)); dc.DrawLine(plume, e(o.C1), e(o.M1));
                dc.DrawLine(plume, e(o.Hanche), e(o.G1)); dc.DrawLine(plume, e(o.G1), e(o.P1));
            }
        }

        public static FormattedText Texte(string texte, double taille, Brush pinceau, bool gras = false)
        {
            return new FormattedText(texte, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI, Segoe UI Symbol"), FontStyles.Normal, gras ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
                taille, pinceau);
        }

        // Accessoires : quelques traits et ronds posés par-dessus le squelette, selon l'animation.
        public static void Objet(DrawingContext dc, string objet, Os o, Func<Point, Point> e, double s, double phase, Color couleur, double epaisseur)
        {
            Pen gris = Plume(Color.FromRgb(0xE8, 0xE8, 0xEE), Math.Max(2, epaisseur * 0.6));
            Pen fin = Plume(Color.FromRgb(0xE8, 0xE8, 0xEE), Math.Max(1.5, epaisseur * 0.35));
            Brush clair = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF8));
            Brush teinte = new SolidColorBrush(couleur);
            Vector avantBras = o.M1 - o.C1;
            if (avantBras.Length > 0.01) avantBras.Normalize();
            Point tete = e(o.Tete);
            double f = 2 * Math.PI * phase;
            switch (objet)
            {
                case "epee": case "club": case "raquette":
                    {
                        double longueur = objet == "epee" ? 48 : objet == "club" ? 52 : 30;
                        Point bout = o.M1 + avantBras * longueur;
                        dc.DrawLine(gris, e(o.M1), e(bout));
                        Vector travers = new Vector(-avantBras.Y, avantBras.X);
                        if (objet == "epee") dc.DrawLine(gris, e(o.M1 + avantBras * 6 + travers * 6), e(o.M1 + avantBras * 6 - travers * 6));
                        else if (objet == "club") dc.DrawLine(gris, e(bout), e(bout + travers * 8));
                        else dc.DrawEllipse(null, fin, e(bout + avantBras * 10), 9 * s, 12 * s);
                        break;
                    }
                case "baton":
                    {
                        Vector axe = new Vector(Math.Cos(2 * f), Math.Sin(2 * f)) * 44;
                        dc.DrawLine(gris, e(o.M1 + axe), e(o.M1 - axe));
                        break;
                    }
                case "baton2": case "haltere":
                    {
                        Vector axe = o.M1 - o.M2;
                        if (axe.Length < 4) axe = new Vector(1, 0);
                        axe.Normalize();
                        double depasse = objet == "haltere" ? 12 : 34;
                        Point a = o.M1 + axe * depasse, b = o.M2 - axe * depasse;
                        dc.DrawLine(gris, e(a), e(b));
                        if (objet == "haltere") { dc.DrawEllipse(clair, null, e(a), 7 * s, 7 * s); dc.DrawEllipse(clair, null, e(b), 7 * s, 7 * s); }
                        break;
                    }
                case "corde":
                    {
                        Point a = e(o.M1), b = e(o.M2), milieu = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2 + 95 * s * Math.Cos(f));
                        var arc = new StreamGeometry();
                        using (StreamGeometryContext g = arc.Open()) { g.BeginFigure(a, false, false); g.QuadraticBezierTo(milieu, b, true, true); }
                        dc.DrawGeometry(null, fin, arc);
                        break;
                    }
                case "ballonpied": case "ballontir": case "ballonroule": case "ballonmain":
                    {
                        Point balle;
                        if (objet == "ballonmain") balle = new Point(o.M1.X + 4, o.M1.Y + 8 + (-o.M1.Y - 16) * Math.Abs(Math.Sin(f)));
                        else if (objet == "ballonpied") { double k = Math.Max(0, phase - 0.42); balle = new Point(o.P1.X + 8 + 520 * k, -7 - 300 * k + 420 * k * k); if (phase < 0.42) balle = new Point(26, -7); }
                        else if (objet == "ballonroule") { double k = Math.Max(0, phase - 0.6); balle = phase < 0.6 ? o.M1 + new Vector(0, 8) : new Point(o.M1.X + 430 * k, -7); }
                        else { double k = Math.Max(0, phase - 0.5); balle = phase < 0.5 ? o.M1 + new Vector(2, -8) : new Point(o.M1.X + 330 * k, o.M1.Y - 8 - 420 * k + 900 * k * k); }
                        dc.DrawEllipse(clair, fin, e(balle), 7 * s, 7 * s);
                        break;
                    }
                case "planche":
                    {
                        Point a = new Point(Math.Min(o.P1.X, o.P2.X) - 12, 0), b = new Point(Math.Max(o.P1.X, o.P2.X) + 12, 0);
                        dc.DrawLine(gris, e(a), e(b));
                        break;
                    }
                case "laptop":
                    {
                        Point a = o.G1 + new Vector(-16, -5), b = o.G1 + new Vector(8, -5);
                        dc.DrawLine(gris, e(a), e(b));
                        dc.DrawLine(gris, e(b), e(b + new Vector(8, -18)));
                        break;
                    }
                case "livre": case "telephone":
                    {
                        Point m = e(o.M1);
                        double l = (objet == "livre" ? 16 : 6) * s, h = (objet == "livre" ? 11 : 11) * s;
                        dc.DrawRectangle(clair, fin, new Rect(m.X - l / 2, m.Y - h, l, h));
                        break;
                    }
                case "crayon":
                    dc.DrawLine(Plume(Color.FromRgb(0xFF, 0xD5, 0x4F), Math.Max(2, epaisseur * 0.5)), e(o.M1), e(o.M1 + avantBras * 14));
                    break;
                case "rayon":
                    if (phase > 0.45 && phase < 0.86)
                    {
                        Point mains = new Point((o.M1.X + o.M2.X) / 2 + 6, (o.M1.Y + o.M2.Y) / 2);
                        double vibre = 1 + 0.25 * Math.Sin(phase * 90);
                        dc.DrawLine(Plume(Color.FromArgb(150, couleur.R, couleur.G, couleur.B), epaisseur * 2.6 * vibre), e(mains), e(mains + new Vector(150, 0)));
                        dc.DrawLine(Plume(Colors.White, epaisseur * 0.9 * vibre), e(mains), e(mains + new Vector(150, 0)));
                    }
                    break;
                case "arc":
                    {
                        Point poing = o.M2;
                        var courbe = new StreamGeometry();
                        using (StreamGeometryContext g = courbe.Open()) { g.BeginFigure(e(poing + new Vector(-4, -26)), false, false); g.QuadraticBezierTo(e(poing + new Vector(14, 0)), e(poing + new Vector(-4, 26)), true, true); }
                        dc.DrawGeometry(null, gris, courbe);
                        double vol = Math.Max(0, phase - 0.52);
                        Point fleche = phase < 0.52 ? new Point(Math.Min(poing.X - 8, o.M1.X), poing.Y) : new Point(poing.X + 700 * vol, poing.Y);
                        if (phase > 0.15) dc.DrawLine(fin, e(fleche), e(fleche + new Vector(30, 0)));
                        if (phase < 0.52) { dc.DrawLine(fin, e(poing + new Vector(-4, -26)), e(o.M1)); dc.DrawLine(fin, e(poing + new Vector(-4, 26)), e(o.M1)); }
                        break;
                    }
                case "shuriken":
                    if (phase > 0.32)
                    {
                        double k = phase - 0.32;
                        Point centre = new Point(o.M1.X + 520 * k, o.M1.Y - 10);
                        Vector branche = new Vector(Math.Cos(40 * k), Math.Sin(40 * k)) * 7, autre = new Vector(-branche.Y, branche.X);
                        dc.DrawLine(gris, e(centre - branche), e(centre + branche));
                        dc.DrawLine(gris, e(centre - autre), e(centre + autre));
                    }
                    break;
                case "bouclier":
                    {
                        var courbe = new StreamGeometry();
                        Point m = o.M1 + new Vector(6, 0);
                        using (StreamGeometryContext g = courbe.Open()) { g.BeginFigure(e(m + new Vector(-6, -24)), false, false); g.QuadraticBezierTo(e(m + new Vector(14, 0)), e(m + new Vector(-6, 24)), true, true); }
                        dc.DrawGeometry(null, Plume(Color.FromRgb(0xE8, 0xE8, 0xEE), Math.Max(3, epaisseur)), courbe);
                        break;
                    }
                case "jongle":
                    for (int i = 0; i < 3; i++)
                    {
                        double k = (phase + i / 3.0) % 1;
                        Point balle = new Point(o.M2.X + (o.M1.X - o.M2.X) * k + 4, Math.Min(o.M1.Y, o.M2.Y) - 6 - 150 * k * (1 - k));
                        dc.DrawEllipse(clair, null, e(balle), 4.5 * s, 4.5 * s);
                    }
                    break;
                case "yoyo":
                    {
                        Point bas = new Point(o.M1.X, o.M1.Y + 8 + 34 * (0.5 - 0.5 * Math.Cos(f)));
                        dc.DrawLine(fin, e(o.M1), e(bas));
                        dc.DrawEllipse(teinte, fin, e(bas), 5 * s, 5 * s);
                        break;
                    }
                case "boule":
                    {
                        Point mains = new Point((o.M1.X + o.M2.X) / 2, (o.M1.Y + o.M2.Y) / 2);
                        double charge = Math.Min(1, phase / 0.55), tir = Math.Max(0, phase - 0.57);
                        Point centre = e(new Point(mains.X + 8 + 700 * tir, mains.Y));
                        double rayon = (4 + 12 * charge) * s;
                        var halo = new RadialGradientBrush(Color.FromArgb(230, 255, 255, 255), Color.FromArgb(0, couleur.R, couleur.G, couleur.B));
                        dc.DrawEllipse(halo, null, centre, rayon * 2.2, rayon * 2.2);
                        dc.DrawEllipse(teinte, null, centre, rayon * 0.7, rayon * 0.7);
                        break;
                    }
                case "onde":
                    if (phase > 0.43)
                    {
                        double k = (phase - 0.43) / 0.57;
                        var plume = Plume(Color.FromArgb((byte)(220 * (1 - k)), couleur.R, couleur.G, couleur.B), epaisseur * 0.7);
                        dc.DrawEllipse(null, plume, e(new Point(o.Hanche.X, -2)), (20 + 150 * k) * s, (4 + 16 * k) * s);
                    }
                    break;
                case "aura":
                    for (int i = 0; i < 7; i++)
                    {
                        double k = (phase * 3 + i * 0.37) % 1, x = o.Hanche.X - 30 + i * 10 + 4 * Math.Sin(i * 2.1);
                        var plume = Plume(Color.FromArgb((byte)(200 * (1 - k)), couleur.R, couleur.G, couleur.B), epaisseur * 0.45);
                        dc.DrawLine(plume, e(new Point(x, -10 - 90 * k)), e(new Point(x, -24 - 90 * k)));
                    }
                    break;
                case "zzz": case "coeur": case "note": case "exclam": case "question":
                    {
                        string signe = objet == "zzz" ? "z" : objet == "coeur" ? "♥" : objet == "note" ? "♪" : objet == "exclam" ? "!" : "?";
                        Brush pinceau = objet == "coeur" ? new SolidColorBrush(Color.FromRgb(0xFF, 0x4D, 0x6D)) : clair;
                        bool monte = objet != "exclam" && objet != "question";
                        for (int i = 0; i < (monte ? 3 : 1); i++)
                        {
                            double k = monte ? (phase + i / 3.0) % 1 : 0.2;
                            var texte = Texte(signe, (objet == "zzz" ? 10 + 8 * k : 18) * s, pinceau, true);
                            texte.SetForegroundBrush(new SolidColorBrush(Color.FromArgb((byte)(255 * (monte ? 1 - k : 1)), ((SolidColorBrush)pinceau).Color.R, ((SolidColorBrush)pinceau).Color.G, ((SolidColorBrush)pinceau).Color.B)));
                            dc.DrawText(texte, new Point(tete.X + (14 + 14 * k) * s, tete.Y - (22 + 34 * k) * s));
                        }
                        break;
                    }
            }
        }
    }

    // ============================================================ sons
    // Aucun fichier audio : chaque bruitage est une petite onde calculée au démarrage (glissés de
    // fréquence, souffle), gardée en mémoire et jouée par Windows.
    static class Sons
    {
        const int Hz = 22050;
        static readonly Dictionary<string, IntPtr> ondes = new Dictionary<string, IntPtr>();
        static double volumeConstruit = -1;
        static uint graine = 12345;

        [DllImport("winmm.dll")]
        static extern bool PlaySound(IntPtr son, IntPtr module, uint drapeaux);

        public static void Jouer(string nom, string groupe)
        {
            if (nom == null || !R.O("sons") || !R.O(groupe)) return;
            double volume = R.D("volume") / 100;
            if (volume <= 0.005) return;
            if (Math.Abs(volume - volumeConstruit) > 0.001) Construire(volume);
            IntPtr onde;
            if (ondes.TryGetValue(nom, out onde)) PlaySound(onde, IntPtr.Zero, 0x0001 | 0x0004 | 0x0002);    // asynchrone, en mémoire, sans son par défaut
        }

        static double Souffle() { graine = graine * 1664525 + 1013904223; return (graine >> 8) / 8388608.0 - 1; }

        static void Construire(double volume)
        {
            PlaySound(IntPtr.Zero, IntPtr.Zero, 0);                                   // rien ne doit jouer pendant qu'on remplace les ondes
            foreach (IntPtr ancienne in ondes.Values) Marshal.FreeHGlobal(ancienne);
            ondes.Clear();
            volumeConstruit = volume;
            // (durée, fréquence au fil du temps, part de souffle, vitesse d'extinction)
            Onde("saut", 0.17, t => 260 + 3000 * t, 0, 9, volume);
            Onde("grandsaut", 0.30, t => 200 + 2400 * t, 0, 5, volume);
            Onde("atterrit", 0.14, t => 150 - 650 * t, 0.35, 22, volume);
            Onde("rebond", 0.15, t => 190 + 2600 * t * (1 - t / 0.15), 0.1, 12, volume);
            Onde("coup", 0.09, t => 110 - 500 * t, 0.7, 30, volume);
            Onde("swish", 0.16, t => 0, 1, 14, volume * 0.6, true);
            Onde("lance", 0.28, t => 0, 1, 7, volume * 0.5, true);
            Onde("attrape", 0.12, t => t < 0.06 ? 520 : 820, 0, 14, volume * 0.8);
            Onde("aie", 0.22, t => 720 - 2100 * t, 0.05, 7, volume);
            Onde("glisse", 0.32, t => 950 - 2000 * t, 0, 5, volume * 0.8);
            Onde("pop", 0.09, t => 800 + 7000 * t, 0, 20, volume * 0.8);
            Onde("note", 0.30, t => t < 0.13 ? 660 : 880, 0, 7, volume * 0.7);
            Onde("tada", 0.42, t => t < 0.1 ? 523 : t < 0.2 ? 659 : t < 0.3 ? 784 : 1047, 0, 4, volume * 0.7);
            Onde("energie", 0.75, t => 110 + 700 * t * t + 25 * Math.Sin(60 * t), 0.15, 1.2, volume * 0.8);
        }

        static void Onde(string nom, double duree, Func<double, double> frequence, double souffle, double extinction, double volume, bool sifflant = false)
        {
            int n = (int)(duree * Hz);
            IntPtr memoire = Marshal.AllocHGlobal(44 + 2 * n);
            var octets = new byte[44 + 2 * n];
            Array.Copy(Encoding.ASCII.GetBytes("RIFF"), 0, octets, 0, 4);
            BitConverter.GetBytes(36 + 2 * n).CopyTo(octets, 4);
            Array.Copy(Encoding.ASCII.GetBytes("WAVEfmt "), 0, octets, 8, 8);
            BitConverter.GetBytes(16).CopyTo(octets, 16);
            BitConverter.GetBytes((short)1).CopyTo(octets, 20);
            BitConverter.GetBytes((short)1).CopyTo(octets, 22);
            BitConverter.GetBytes(Hz).CopyTo(octets, 24);
            BitConverter.GetBytes(Hz * 2).CopyTo(octets, 28);
            BitConverter.GetBytes((short)2).CopyTo(octets, 32);
            BitConverter.GetBytes((short)16).CopyTo(octets, 34);
            Array.Copy(Encoding.ASCII.GetBytes("data"), 0, octets, 36, 4);
            BitConverter.GetBytes(2 * n).CopyTo(octets, 40);
            double angle = 0, filtre = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Hz;
                angle += 2 * Math.PI * Math.Max(0, frequence(t)) / Hz;
                double bruit = Souffle();
                // un « swish » : du souffle dont on ne garde que l'aigu, qui enfle puis retombe
                filtre += (bruit - filtre) * (sifflant ? 0.5 : 0.18);
                double enveloppe = sifflant ? Math.Sin(Math.PI * t / duree) : Math.Min(1, t / 0.004) * Math.Exp(-extinction * t);
                double ton = Math.Sin(angle) + 0.3 * Math.Sin(2 * angle);
                double valeur = ((1 - souffle) * ton * 0.75 + souffle * (sifflant ? bruit - filtre : filtre) * 1.4) * enveloppe * volume;
                if (i > n - 200) valeur *= (n - i) / 200.0;                              // pas de claquement à la fin
                BitConverter.GetBytes((short)(Math.Max(-1, Math.Min(1, valeur)) * 30000)).CopyTo(octets, 44 + 2 * i);
            }
            Marshal.Copy(octets, 0, memoire, octets.Length);
            ondes[nom] = memoire;
            if (export != null) File.WriteAllBytes(Path.Combine(export, nom + ".wav"), octets);
        }

        // MascotteStickman.exe --sons dossier : écrit les bruitages en .wav, pour les écouter ou les contrôler.
        static string export;
        public static void Exporter(string dossier)
        {
            Directory.CreateDirectory(dossier);
            export = dossier;
            Construire(R.D("volume") / 100);
            export = null;
        }
    }

    sealed class Toile : FrameworkElement
    {
        public Action<DrawingContext> Peindre;
        protected override void OnRender(DrawingContext dc) { if (Peindre != null) Peindre(dc); }
    }

    // Planches de contrôle : chaque animation en huit images, pour vérifier les poses d'un coup d'œil.
    static class Planches
    {
        public static void Ecrire(string dossier, string filtre)
        {
            Directory.CreateDirectory(dossier);
            List<Anim> liste = Biblio.Toutes.Where(a => filtre == "" || (a.Famille + " " + a.Nom).IndexOf(filtre, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            const int parPage = 12, images = 8;
            const double cl = 100, ch = 108, marge = 190, echelle = 0.55;
            Pen plume = Dessin.Plume(Color.FromRgb(0xFF, 0x8A, 0x1A), 3.5);
            for (int page = 0; page * parPage < liste.Count; page++)
            {
                var visuel = new DrawingVisual();
                using (DrawingContext dc = visuel.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x24, 0x26, 0x2B)), null, new Rect(0, 0, marge + images * cl, parPage * ch));
                    for (int i = 0; i < parPage && page * parPage + i < liste.Count; i++)
                    {
                        Anim a = liste[page * parPage + i];
                        dc.DrawText(Dessin.Texte(a.Famille, 10, Brushes.Gray), new Point(6, i * ch + 34));
                        dc.DrawText(Dessin.Texte(a.Nom, 12, Brushes.White), new Point(6, i * ch + 48));
                        dc.DrawLine(new Pen(Brushes.DimGray, 1), new Point(marge, (i + 1) * ch - 6), new Point(marge + images * cl, (i + 1) * ch - 6));
                        for (int k = 0; k < images; k++)
                        {
                            double phase = k / (double)images;
                            double[] p = a.Pose(phase);
                            if (a.Haut) p = Bonhomme.Superposer(Biblio.Neutre(), p);
                            Dessin.Os o = Dessin.Calculer(p, false);
                            double ox = marge + k * cl + cl / 2, oy = (i + 1) * ch - 6;
                            Func<Point, Point> e = point => new Point(ox + point.X * echelle, oy + point.Y * echelle);
                            Dessin.Tracer(dc, o, e, echelle, null, plume, null);
                            if (a.Objet != null) Dessin.Objet(dc, a.Objet, o, e, echelle, phase, Color.FromRgb(0xFF, 0x8A, 0x1A), 3.5);
                        }
                    }
                }
                var image = new RenderTargetBitmap((int)(marge + images * cl), (int)(parPage * ch), 96, 96, PixelFormats.Pbgra32);
                image.Render(visuel);
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(image));
                using (FileStream flux = File.Create(Path.Combine(dossier, "planche-" + (page + 1).ToString("00") + ".png"))) png.Save(flux);
            }
            File.WriteAllText(Path.Combine(dossier, "compte.txt"), Biblio.Toutes.Count + " animations, " + R.Tous.Count(p => p.Cat != "") + " réglages généraux");
        }
    }

    // ============================================================ la mascotte

    sealed class Bonhomme : Window
    {
        enum Etat { Anime, Vol, Porte }

        // Dans la série, la plupart des stick figures ont la tête pleine ; ceux nés de l'animateur
        // (The Second Coming, The Chosen One, The Dark Lord) ont la tête creuse.
        public sealed class Nuance { public string Nom; public int Rvb; public bool Creuse; }

        public static readonly Nuance[] Palette =
        {
            Teinte("Orange (The Second Coming)", 0xFF8A1A, true), Teinte("Rouge", 0xE53935), Teinte("Vert", 0x43A047), Teinte("Bleu", 0x1E88E5),
            Teinte("Jaune", 0xFDD835), Teinte("Violet", 0x8E24AA), Teinte("Rose", 0xEC407A), Teinte("Cyan", 0x00BCD4),
            Teinte("Noir (The Chosen One)", 0x111111, true), Teinte("Rouge sombre (The Dark Lord)", 0xB71C1C, true), Teinte("Blanc", 0xFFFFFF), Teinte("Gris", 0x9E9E9E),
            Teinte("Turquoise", 0x26A69A), Teinte("Or", 0xFFC107), Teinte("Citron vert", 0xC6FF00), Teinte("Bleu nuit", 0x3949AB),
        };
        static Nuance Teinte(string nom, int rvb, bool creuse = false) { return new Nuance { Nom = nom, Rvb = rvb, Creuse = creuse }; }

        // Tête creuse ou pleine : réglage forcé, sinon celle de la teinte de la palette la plus proche.
        static bool TeteCreuse(Color couleur)
        {
            int style = (int)R.D("styleTete");
            if (style != 0) return style == 2;
            if (R.O("arcenciel")) return false;
            Nuance proche = null;
            double meilleur = double.MaxValue;
            foreach (Nuance n in Palette)
            {
                Color c = R.Rvb(n.Rvb);
                double ecart = Math.Pow(c.R - couleur.R, 2) + Math.Pow(c.G - couleur.G, 2) + Math.Pow(c.B - couleur.B, 2);
                if (ecart < meilleur) { meilleur = ecart; proche = n; }
            }
            return proche.Creuse;
        }

        readonly Toile toile = new Toile { Cursor = Cursors.Hand };
        readonly Random hasard = new Random();
        readonly Stopwatch chrono = Stopwatch.StartNew();
        readonly DispatcherTimer minuteur = new DispatcherTimer(DispatcherPriority.Render);
        readonly List<Point> trace = new List<Point>();
        Parametres fenetreReglages;

        Etat etat = Etat.Anime;
        Anim courante, geste;
        double tCourante, tGeste, toursCourante, toursGeste, phase;
        Action apres;
        bool enRepos, dort;
        double[] pose = Biblio.Neutre(), fonduDepuis;
        double fondu = 1;

        double s = 1, largeur, hauteur, solY;        // taille et dimensions de la fenêtre
        Point ancre, maison;                          // point au sol sous lui, et sa place habituelle
        int face = 1;
        double cible; bool versCible;
        Vector vitesse; double vrille, rotVol; int rebonds; bool sautVoulu;
        IntPtr poignee, support; double supportX;
        double temps, prochaineAction, finCombat, prochaineCommande, prochaineSauvegarde;

        Point curseur; Vector vCurseur; bool appui, curseurLu; Point appuiCurseur;
        double balance, vBalance;
        string bulle; double finBulle;
        bool sonFait;                                 // le bruitage de l'animation en cours a déjà été joué
        int styleSaut; double tVol, dureeVol;         // saut vers une fenêtre : 0 simple, 1 en salto, 2 atterrissage de héros
        double voile = 1, voileVise = 1;              // fondu de la téléportation

        public Bonhomme()
        {
            Title = "Mascotte Stickman";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Content = toile;
            toile.Peindre = Dessiner;
            toile.MouseLeftButtonDown += (o, e) => { appui = true; appuiCurseur = curseur; toile.CaptureMouse(); };
            toile.MouseLeftButtonUp += (o, e) => Relacher();
            ConstruireMenu();

            Rect zone = SystemParameters.WorkArea;
            maison = new Point(zone.Right - R.D("droite"), zone.Bottom);
            maison.X = Math.Max(SystemParameters.VirtualScreenLeft + 60, Math.Min(SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 60, maison.X));
            ancre = maison;
            Appliquer();
            Repos();
            minuteur.Tick += Tic;
            Loaded += (o, e) =>
            {
                minuteur.Start();
                Jouer(Biblio.Salut, 4);
                Dire("Salut !", 3);
            };
            Closed += (o, e) => { R.Enregistrer(); if (fenetreReglages != null) fenetreReglages.Close(); };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            poignee = new WindowInteropHelper(this).Handle;
            SetWindowLong(poignee, GWL_EXSTYLE, GetWindowLong(poignee, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
        }

        // Les réglages qui touchent la fenêtre elle-même ; les autres sont relus à chaque image.
        public void Appliquer()
        {
            s = R.D("taille");
            largeur = 340 * s; hauteur = 360 * s; solY = hauteur - 64 * s;      // sous le sol : la place des jambes qui pendent d'une fenêtre
            Width = largeur; Height = hauteur;
            Topmost = R.O("premierplan");
            minuteur.Interval = TimeSpan.FromMilliseconds(1000 / R.D("fluidite"));
            toile.Effect = R.O("lueur") ? new DropShadowEffect { Color = CouleurDuMoment(), BlurRadius = 16 * s, ShadowDepth = 0, Opacity = 0.95 } : null;
        }

        Color CouleurDuMoment()
        {
            if (!R.O("arcenciel")) return R.Couleur("couleur");
            double h = temps * R.D("arcVitesse") % 360 / 60, x = 1 - Math.Abs(h % 2 - 1);
            double r = h < 1 ? 1 : h < 2 ? x : h < 4 ? 0 : h < 5 ? x : 1, v = h < 1 ? x : h < 3 ? 1 : h < 4 ? x : 0, b = h < 2 ? 0 : h < 3 ? x : h < 5 ? 1 : x;
            return Color.FromRgb((byte)(255 * r), (byte)(255 * v), (byte)(255 * b));
        }

        // ------------------------------------------------------------ boucle principale

        void Tic(object o, EventArgs e)
        {
            double maintenant = chrono.Elapsed.TotalSeconds, dt = Math.Min(0.05, maintenant - temps);
            temps = maintenant;
            LireCurseur(dt);
            if (appui && etat != Etat.Porte && R.O("attrape") && (curseur - appuiCurseur).Length > 6) Attraper();
            if (temps > prochaineCommande) { prochaineCommande = temps + 0.4; LireCommande(); }

            switch (etat)
            {
                case Etat.Anime: Animer(dt); break;
                case Etat.Vol: Voler(dt); break;
                case Etat.Porte: Porter(dt); break;
            }
            voile += Math.Max(-dt * 5, Math.Min(dt * 5, voileVise - voile));
            toile.Opacity = R.D("opacite") / 100 * voile;
            Placer();
            toile.InvalidateVisual();
            if (R.Sale && temps > prochaineSauvegarde) { prochaineSauvegarde = temps + 2; R.Enregistrer(); }
        }

        void LireCurseur(double dt)
        {
            POINT p;
            GetCursorPos(out p);
            PresentationSource source = PresentationSource.FromVisual(this);
            Point point = (source != null ? source.CompositionTarget.TransformFromDevice : Matrix.Identity).Transform(new Point(p.X, p.Y));
            if (curseurLu && dt > 0) vCurseur = vCurseur * 0.6 + (point - curseur) / dt * 0.4;
            curseur = point;
            curseurLu = true;
        }

        void LireCommande()
        {
            string fichier = Path.Combine(Programme.Dossier, "commande.txt");
            if (!File.Exists(fichier)) return;
            try
            {
                string nom = File.ReadAllText(fichier).Trim();
                File.Delete(fichier);
                // quelques commandes en plus des noms d'animations : @reglages, @fenetre, @couleur RRVVBB
                if (nom == "@reglages") { OuvrirReglages(); return; }
                if (nom == "@fenetre") { if (etat == Etat.Anime) SauterSurFenetre(); return; }
                if (nom.StartsWith("@couleur ")) { R.Mettre("couleur", Convert.ToInt32(nom.Substring(9).Trim(), 16)); R.Mettre("arcenciel", 0); Appliquer(); return; }
                Anim a = Biblio.Trouver(nom);
                if (a != null && etat == Etat.Anime && (!a.SurFenetre || support != IntPtr.Zero)) Dire(a.Nom, 2.5);
                if (a != null) JouerDemande(a);
            }
            catch (IOException) { }
        }

        void Placer()
        {
            if (etat == Etat.Porte) { Left = curseur.X - largeur / 2; Top = curseur.Y - hauteur * 0.2; }
            else { Left = ancre.X - largeur / 2; Top = ancre.Y - solY; }
        }

        // ------------------------------------------------------------ animations

        void Fondre()
        {
            fonduDepuis = (double[])pose.Clone();
            fondu = 0;
        }

        public void Jouer(Anim a, double tours, Action ensuite = null)
        {
            if (a == null) return;
            if (a.Haut)                                          // geste du haut du corps : par-dessus ce que font les jambes
            {
                geste = a; tGeste = 0; toursGeste = tours;
                Sons.Jouer(a.Son, "sonsAnimations");
                return;
            }
            Fondre();
            courante = a; tCourante = 0; toursCourante = tours; apres = ensuite;
            versCible = false; enRepos = false; dort = false; sonFait = false;
        }

        void JouerDemande(Anim a)
        {
            if (etat != Etat.Anime) return;
            if (a.SurFenetre && support == IntPtr.Zero)          // jambes dans le vide : il lui faut un rebord
            {
                Dire(SauterSurFenetre() ? "Il me faut une fenêtre !" : "Aucune fenêtre où m'asseoir", 2.5);
                return;
            }
            if (a.Deplace) AllerVers(Destination(), a, null);
            else Jouer(a, Math.Max(1, Math.Round(a.Tours * R.D("tours") / 100)));
        }

        void Repos()
        {
            Jouer(Biblio.Repos[hasard.Next(Biblio.Repos.Count)], double.MaxValue);
            enRepos = true;
            prochaineAction = temps + R.D("activite") * (0.5 + hasard.NextDouble());
        }

        void Suite()
        {
            Action a = apres;
            apres = null;
            if (a != null) a(); else Repos();
        }

        void AllerVers(double x, Anim marche, Action ensuite)
        {
            double gauche, droite;
            Limites(out gauche, out droite);
            x = Math.Max(gauche, Math.Min(droite, x));
            if (Math.Abs(x - ancre.X) < 8) { if (ensuite != null) ensuite(); else Repos(); return; }
            Jouer(marche, double.MaxValue, ensuite);
            cible = x; versCible = true;
            face = (x > ancre.X ? 1 : -1) * (marche.Vitesse < 0 ? -1 : 1);
        }

        double Destination()
        {
            double centre = R.O("partout") || support != IntPtr.Zero ? ancre.X : maison.X;
            double distance = R.D("distance") * (0.3 + 0.7 * hasard.NextDouble());
            double gauche, droite;
            Limites(out gauche, out droite);
            double x = centre + (hasard.Next(2) == 0 ? -distance : distance);
            if (x < gauche || x > droite) x = centre - (x - centre);
            return Math.Max(gauche, Math.Min(droite, x));
        }

        void Limites(out double gauche, out double droite)
        {
            Bord bord;
            if (support != IntPtr.Zero && LireBord(support, out bord)) { gauche = bord.Gauche + 16; droite = bord.Droite - 16; return; }
            gauche = SystemParameters.VirtualScreenLeft + 30 * s;
            droite = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 30 * s;
        }

        // Applique un geste du haut du corps sur une pose complète.
        public static double[] Superposer(double[] corps, double[] haut)
        {
            var p = (double[])corps.Clone();
            p[I.Torse] += haut[I.Torse];
            p[I.Tete] = haut[I.Tete];
            p[I.Ep1] = haut[I.Ep1]; p[I.Co1] = haut[I.Co1]; p[I.Ep2] = haut[I.Ep2]; p[I.Co2] = haut[I.Co2];
            return p;
        }

        double[] Adoucir(double[] p, double dt)
        {
            if (fondu >= 1) return p;
            double duree = R.D("fondu") / 1000;
            fondu = duree <= 0.001 ? 1 : fondu + dt / duree;
            if (fondu >= 1) return p;
            double k = fondu * fondu * (3 - 2 * fondu);
            double[] q = Biblio.Mix(fonduDepuis, p, k);
            double ecart = Math.IEEERemainder(p[I.Rot] - fonduDepuis[I.Rot], 360);     // la rotation prend le plus court chemin
            q[I.Rot] = fonduDepuis[I.Rot] + ecart * k;
            return q;
        }

        void Animer(double dt)
        {
            if (SupportPerdu()) return;
            if (Bouscule()) return;

            tCourante += dt * R.D("vitesse") / 100 / courante.Duree;
            if (courante.Deplace && versCible)
            {
                double pas = Math.Abs(courante.Vitesse) * R.D("marche") / 100 * s * dt;
                if (Math.Abs(cible - ancre.X) <= pas) { Glisser(cible - ancre.X); Suite(); return; }
                Glisser(cible > ancre.X ? pas : -pas);
            }
            else
            {
                if (courante.Vitesse != 0) Glisser(face * courante.Vitesse * s * dt, true);
                if (tCourante >= toursCourante) { Suite(); return; }
            }

            phase = tCourante - Math.Floor(tCourante);
            if (!sonFait && courante.Son != null && tCourante >= courante.SonA) { sonFait = true; Sons.Jouer(courante.Son, "sonsAnimations"); }
            double[] p = courante.Pose(phase);
            if (geste != null)
            {
                tGeste += dt * R.D("vitesse") / 100 / geste.Duree;
                if (tGeste >= toursGeste) { geste = null; Fondre(); }
                else p = Superposer(p, geste.Pose(tGeste - Math.Floor(tGeste)));
            }
            if (enRepos && geste == null && R.O("regarde")) p[I.Tete] += Regard();
            p[I.Rot] = Math.IEEERemainder(p[I.Rot], 360);
            pose = Adoucir(p, dt);

            if (enRepos) Vivre();
        }

        void Glisser(double dx, bool borne = false)
        {
            if (borne)
            {
                double gauche, droite;
                Limites(out gauche, out droite);
                dx = Math.Max(gauche, Math.Min(droite, ancre.X + dx)) - ancre.X;
            }
            ancre.X += dx;
            if (support != IntPtr.Zero) supportX += dx;
        }

        double Regard()
        {
            Vector vers = curseur - new Point(ancre.X, ancre.Y - 95 * s);
            if (vers.X * face < 20) return 0;                    // le curseur est derrière : il ne se tord pas le cou
            return Math.Max(-35, Math.Min(30, Math.Atan2(vers.Y, Math.Abs(vers.X)) * 180 / Math.PI * 0.7));
        }

        // Ce qu'il décide de faire quand il n'a rien en cours.
        void Vivre()
        {
            double dx = curseur.X - ancre.X, haut = ancre.Y - curseur.Y;
            if (!appui)
            {
                if (R.O("combat") && temps > finCombat && Math.Abs(dx) < R.D("portee") * s && Math.Abs(dx) > 14 * s && haut > -20 && haut < 175 * s)
                {
                    face = dx > 0 ? 1 : -1;
                    finCombat = temps + R.D("combatRepos") * (0.6 + 0.8 * hasard.NextDouble());
                    Jouer(haut > 100 * s ? Biblio.PoingHaut : haut > 62 * s ? Biblio.Poing : haut > 30 * s ? Biblio.PiedMoyen : Biblio.PiedBas, 1);
                    return;
                }
                if (R.O("fuit") && Math.Abs(dx) < 170 * s && haut > -40 && haut < 260 * s) { AllerVers(ancre.X - Math.Sign(dx) * 320 * s, Biblio.Course, null); if (!enRepos) return; }
                if (R.O("suit") && Math.Abs(dx) > 150 * s) { AllerVers(curseur.X - Math.Sign(dx) * 80 * s, Math.Abs(dx) > 500 * s ? Biblio.Course : Biblio.Marche, null); if (!enRepos) return; }
            }

            double inactif = SecondesSansSaisie();
            if (R.D("sommeil") > 0 && inactif > R.D("sommeil") * 60 && !dort)
            {
                Jouer(Biblio.Dort, double.MaxValue);
                enRepos = true; dort = true;
                prochaineAction = double.MaxValue;
                return;
            }
            if (dort) { if (inactif < 2) { Jouer(Biblio.SeReleve, 1); Dire("Hein ?!", 2); } return; }
            if (temps >= prochaineAction) Choisir();
        }

        void Choisir()
        {
            if (R.O("fenetres") && hasard.NextDouble() * 100 < R.D("fenetresChance") && SauterSurFenetre()) return;
            if (hasard.NextDouble() * 100 < R.D("teleporte") && Teleporter()) return;

            // les animations « jambes dans le vide » ne sont proposées que perché sur une fenêtre
            Func<Anim, bool> permise = a => !R.Coupees.Contains(a.Nom) && (!a.SurFenetre || support != IntPtr.Zero);
            var possibles = new List<Anim>();
            double total = 0;
            var poids = new Dictionary<string, double>();
            foreach (string famille in Biblio.Familles)
            {
                double w = famille == "Déplacements" && !R.O("balade") ? 0 : R.D("poids." + famille);
                if (famille == "Fenêtres" && support != IntPtr.Zero) w *= 3;      // et là, il en profite
                poids[famille] = w;
                if (w > 0 && Biblio.Toutes.Any(a => a.Famille == famille && permise(a))) total += w;
                else poids[famille] = 0;
            }
            if (total <= 0) { prochaineAction = temps + 2; return; }
            double tirage = hasard.NextDouble() * total;
            string choisie = null;
            foreach (string famille in Biblio.Familles)
            {
                if (poids[famille] <= 0) continue;
                choisie = famille;
                tirage -= poids[famille];
                if (tirage <= 0) break;
            }
            foreach (Anim a in Biblio.Toutes) if (a.Famille == choisie && permise(a)) possibles.Add(a);
            Anim suivante = possibles[hasard.Next(possibles.Count)];
            if (suivante.Haut) { Jouer(suivante, suivante.Tours); prochaineAction = temps + suivante.Duree * suivante.Tours + R.D("activite") * (0.5 + hasard.NextDouble()); return; }
            JouerDemande(suivante);
            // un geste en marchant, de temps en temps
            if (suivante.Deplace && hasard.NextDouble() * 100 < R.D("enchaine"))
            {
                List<Anim> gestes = Biblio.Toutes.Where(a => a.Haut && !R.Coupees.Contains(a.Nom)).ToList();
                if (gestes.Count > 0) { Anim g = gestes[hasard.Next(gestes.Count)]; Jouer(g, g.Tours); }
            }
        }

        public void Dire(string texte, double secondes)
        {
            if (!R.O("bulles")) return;
            bulle = texte;
            finBulle = temps + secondes;
        }

        // ------------------------------------------------------------ souris : clic, porté, lancé, bousculé

        void Relacher()
        {
            toile.ReleaseMouseCapture();
            bool portait = etat == Etat.Porte;
            appui = false;
            if (portait)
            {
                // il repart du point où ses pieds se trouvaient, avec l'élan du geste
                Dessin.Os o = Dessin.Calculer(pose, true);
                double bas = Dessin.Points(o).Max(p => p.Y);
                ancre = new Point(curseur.X, curseur.Y + bas * s);
                maison = new Point(ancre.X, maison.Y);
                R.Mettre("droite", SystemParameters.WorkArea.Right - maison.X);
                Vector elan = R.O("lancer") ? vCurseur * (R.D("force") / 100) : new Vector(0, 0);
                if (elan.Length > 4200) elan *= 4200 / elan.Length;
                if (elan.Length > 700) Sons.Jouer("lance", "sonsSouris");
                Lancer(elan, false, elan.X * 0.5);
                return;
            }
            if (etat != Etat.Anime) return;
            Anim a;
            switch ((int)R.D("clic"))
            {
                case 0: a = Biblio.Salut; break;
                case 1: a = Biblio.Trouver("Saut"); break;
                case 2: a = Biblio.Danse; break;
                case 3: a = Biblio.Trouver("Enchaînement de 3 coups"); break;
                default:
                    List<Anim> libres = Biblio.Toutes.Where(x => !x.Deplace && !R.Coupees.Contains(x.Nom)).ToList();
                    a = libres.Count > 0 ? libres[hasard.Next(libres.Count)] : Biblio.Salut;
                    break;
            }
            JouerDemande(a);
            if (R.O("bulles") && (int)R.D("clic") == 4) Dire(a.Nom, 2.5);
        }

        void Attraper()
        {
            etat = Etat.Porte;
            support = IntPtr.Zero;
            geste = null; dort = false;
            balance = 0; vBalance = 0;
            voileVise = 1;
            Sons.Jouer("attrape", "sonsSouris");
            Fondre();
        }

        // Tenu par la tête : le corps pend et se balance derrière le curseur.
        void Porter(double dt)
        {
            double force = R.D("balancier") / 100;
            double visee = Math.Max(-80, Math.Min(80, -vCurseur.X * 0.045 * force));
            vBalance += ((visee - balance) * 60 - vBalance * 7) * dt;
            balance += vBalance * dt;
            var p = new double[I.N];
            p[I.Rot] = balance;
            p[I.Tete] = -balance * 0.25;
            double flotte = 8 * Math.Sin(temps * 3) * force, secousse = Math.Min(30, Math.Abs(vBalance) * 0.08);
            p[I.Ep1] = 22 + balance * 0.5 + flotte + secousse; p[I.Co1] = 14;
            p[I.Ep2] = -18 + balance * 0.5 - flotte - secousse; p[I.Co2] = -10;
            p[I.Ha1] = 12 + balance * 0.55 - flotte * 0.6; p[I.Ge1] = -18 - secousse;
            p[I.Ha2] = -8 + balance * 0.55 + flotte * 0.6; p[I.Ge2] = -30 - secousse;
            pose = Adoucir(p, dt);
        }

        void Lancer(Vector elan, bool voulu, double tournoie)
        {
            etat = Etat.Vol;
            vitesse = elan;
            vrille = tournoie * R.D("tournoie") / 100;
            rotVol = pose[I.Rot];
            rebonds = 0;
            sautVoulu = voulu;
            support = IntPtr.Zero;
            geste = null; enRepos = false; dort = false;
            Fondre();
        }

        // Un curseur qui le traverse à toute vitesse l'envoie valser.
        bool Bouscule()
        {
            if (!R.O("bouscule") || appui || vCurseur.Length < 2600) return false;
            double dx = curseur.X - ancre.X, haut = ancre.Y - curseur.Y;
            if (Math.Abs(dx) > 34 * s || haut < 0 || haut > 125 * s) return false;
            Vector elan = vCurseur * 0.4 * (R.D("force") / 100) + new Vector(0, -320);
            if (elan.Length > 3000) elan *= 3000 / elan.Length;
            Sons.Jouer("aie", "sonsSouris");
            Lancer(elan, false, elan.X * 0.6);
            Dire("Aïe !", 1.5);
            return true;
        }

        void Voler(double dt)
        {
            double g = 2300 * s * R.D("gravite") / 100;
            Point avant = ancre;
            vitesse.Y += g * dt;
            ancre += vitesse * dt;
            rotVol += vrille * dt;

            double gauche = SystemParameters.VirtualScreenLeft + 20 * s, droite = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 20 * s;
            if (ancre.X < gauche || ancre.X > droite)
            {
                ancre.X = Math.Max(gauche, Math.Min(droite, ancre.X));
                vitesse.X = R.O("murs") ? -vitesse.X * 0.6 : 0;
                vrille = -vrille * 0.6;
            }
            if (ancre.Y < SystemParameters.VirtualScreenTop + 130 * s && vitesse.Y < 0 && !sautVoulu) vitesse.Y *= 0.5;   // pas plus haut que l'écran

            if (vitesse.Y > 0)
            {
                double sol = SystemParameters.WorkArea.Bottom;
                IntPtr fenetre = IntPtr.Zero;
                if (R.O("fenetres"))
                    foreach (Bord bord in Bords())
                        if (avant.Y <= bord.Haut + 1 && ancre.Y >= bord.Haut && bord.Haut < sol - 40
                            && ancre.X > bord.Gauche + 6 && ancre.X < bord.Droite - 6 && BordLibre(bord, ancre.X))
                        { sol = bord.Haut; fenetre = bord.Fenetre; }
                if (ancre.Y >= sol)
                {
                    ancre.Y = sol;
                    double rebond = R.D("rebond") / 100;
                    if (!sautVoulu && vitesse.Y > 520 * s && rebonds < 3 && rebond > 0.03)
                    {
                        vitesse.Y = -vitesse.Y * rebond;
                        vitesse.X *= 1 - R.D("frottement") / 100 * 0.7;
                        vrille *= 0.5;
                        rebonds++;
                        Sons.Jouer("rebond", "sonsSauts");
                    }
                    else { Atterrir(fenetre); return; }
                }
            }

            double[] p;
            tVol += dt;
            if (sautVoulu && styleSaut == 1 && dureeVol > 0)
            {
                p = (double[])Biblio.Groupe.Clone();             // en salto : un tour complet, groupé, le temps du vol
                p[I.Rot] = 360 * Math.Min(1, tVol / dureeVol);
            }
            else if (sautVoulu) p = (double[])(vitesse.Y < 0 ? Biblio.SautMonte : Biblio.SautDescend).Clone();
            else
            {
                p = new double[I.N];
                double agite = Math.Sin(temps * 14);
                p[I.Rot] = Math.IEEERemainder(rotVol, 360);
                p[I.Ep1] = 120 + 40 * agite; p[I.Co1] = 30; p[I.Ep2] = -130 - 40 * agite; p[I.Co2] = -30;
                p[I.Ha1] = 40 + 25 * agite; p[I.Ge1] = -50; p[I.Ha2] = -30 - 25 * agite; p[I.Ge2] = -60;
            }
            pose = Adoucir(p, dt);
        }

        void Atterrir(IntPtr fenetre)
        {
            etat = Etat.Anime;
            support = fenetre;
            Bord bord;
            if (support != IntPtr.Zero && LireBord(support, out bord)) supportX = ancre.X - bord.Gauche; else support = IntPtr.Zero;
            Sons.Jouer("atterrit", "sonsSauts");
            if (sautVoulu) Jouer(styleSaut == 2 ? Biblio.Heros : Biblio.Reception, 1);
            else
            {
                face = vitesse.X < 0 ? -1 : 1;
                Jouer(Biblio.SeReleve, 1);
            }
        }

        // ------------------------------------------------------------ dessin

        void Dessiner(DrawingContext dc)
        {
            Color couleur = CouleurDuMoment();
            double epaisseur = R.D("epaisseur") * s, bord = R.D("contour") * s;
            Pen trait = Dessin.Plume(couleur, epaisseur);
            Pen contour = bord > 0.2 ? Dessin.Plume(R.Couleur("contourCouleur"), epaisseur + 2 * bord) : null;
            bool porte = etat == Etat.Porte;
            Dessin.Os o = Dessin.Calculer(pose, porte);
            double ox = largeur / 2, oy = porte ? hauteur * 0.2 : solY;
            int sens = face;
            double taille = s;
            Func<Point, Point> e = p => new Point(ox + sens * p.X * taille, oy + p.Y * taille);

            // zone sensible, presque invisible : les traits seuls seraient trop fins à viser
            Point[] points = Dessin.Points(o).Select(e).ToArray();
            double x0 = points.Min(p => p.X) - 14 * s, x1 = points.Max(p => p.X) + 14 * s, y0 = points.Min(p => p.Y) - (o.Rayon + 12) * s, y1 = points.Max(p => p.Y) + 12 * s;
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), null, new Rect(x0, y0, x1 - x0, y1 - y0), 20, 20);

            if (R.O("ombre") && etat == Etat.Anime)
            {
                double h = pose[I.Air];
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)Math.Max(10, 70 - h * 0.5), 0, 0, 0)), null,
                    new Point(e(o.Hanche).X, solY + 2 * s), Math.Max(8, 24 - h * 0.1) * s, 4 * s);
            }

            string objet = etat == Etat.Anime && R.O("objets") ? (geste != null && geste.Objet != null ? geste.Objet : courante.Objet) : null;
            double phaseObjet = geste != null && geste.Objet != null ? tGeste - Math.Floor(tGeste) : phase;

            // traînée : les dernières positions de la main, à l'écran (la fenêtre, elle, se déplace)
            bool dessine = objet == "crayon";
            if (R.O("trainee") || dessine)
            {
                Point main = e(o.M1);
                trace.Add(new Point(main.X + Left, main.Y + Top));
                int longueur = dessine ? 70 : (int)R.D("traineeLongueur");
                while (trace.Count > longueur) trace.RemoveAt(0);
                for (int i = 1; i < trace.Count; i++)
                {
                    byte alpha = (byte)(dessine ? 230 : 200 * i / trace.Count);
                    Color c = dessine ? Color.FromRgb(0xFF, 0xFF, 0xFF) : couleur;
                    dc.DrawLine(Dessin.Plume(Color.FromArgb(alpha, c.R, c.G, c.B), epaisseur * (dessine ? 0.35 : 0.5)),
                        new Point(trace[i - 1].X - Left, trace[i - 1].Y - Top), new Point(trace[i].X - Left, trace[i].Y - Top));
                }
            }
            else if (trace.Count > 0) trace.Clear();

            Dessin.Tracer(dc, o, e, s, contour, trait, TeteCreuse(couleur) ? null : trait.Brush);
            if (objet != null) Dessin.Objet(dc, objet, o, e, s, phaseObjet, couleur, epaisseur);

            if (bulle != null && temps < finBulle)
            {
                FormattedText texte = Dessin.Texte(bulle, 12.5, new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30)));
                texte.MaxTextWidth = Math.Max(60, largeur - 30);
                Point tete = e(o.Tete);
                double l = texte.Width + 18, h = texte.Height + 9;
                double bx = Math.Max(4, Math.Min(largeur - l - 4, tete.X - l / 2)), by = Math.Max(4, tete.Y - (o.Rayon + 14) * s - h);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0xFA, 0xF9, 0xF5)), new Pen(new SolidColorBrush(Color.FromRgb(0xD8, 0xD4, 0xC8)), 1), new Rect(bx, by, l, h), 9, 9);
                dc.DrawText(texte, new Point(bx + 9, by + 4));
            }
        }

        // ------------------------------------------------------------ menu

        void ConstruireMenu()
        {
            var menu = new ContextMenu();
            var reglages = new MenuItem { Header = "Paramètres…  (" + Biblio.Toutes.Count + " animations)" };
            reglages.Click += (o, e) => OuvrirReglages();
            menu.Items.Add(reglages);

            var couleurs = new MenuItem { Header = "Couleur" };
            foreach (Nuance teinte in Palette)
            {
                int rvb = teinte.Rvb;
                var ligne = new StackPanel { Orientation = Orientation.Horizontal };
                // la pastille montre aussi la tête qu'aura le stickman : anneau ou disque
                ligne.Children.Add(new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = teinte.Creuse ? Brushes.Transparent : new SolidColorBrush(R.Rvb(rvb)), BorderBrush = new SolidColorBrush(R.Rvb(rvb)), BorderThickness = new Thickness(3), Margin = new Thickness(0, 0, 8, 0) });
                ligne.Children.Add(new TextBlock { Text = teinte.Nom + (teinte.Creuse ? "  — tête creuse" : "") });
                var choix = new MenuItem { Header = ligne };
                choix.Click += (o, e) => { R.Mettre("couleur", rvb); R.Mettre("arcenciel", 0); Appliquer(); };
                couleurs.Items.Add(choix);
            }
            var arc = new MenuItem { Header = "Arc-en-ciel" };
            arc.Click += (o, e) => { R.Mettre("arcenciel", 1); Appliquer(); };
            couleurs.Items.Add(arc);
            menu.Items.Add(couleurs);

            var animations = new MenuItem { Header = "Jouer une animation" };
            foreach (string famille in Biblio.Familles)
            {
                List<Anim> liste = Biblio.Toutes.Where(a => a.Famille == famille).ToList();
                var sous = new MenuItem { Header = famille + "  (" + liste.Count + ")" };
                foreach (Anim a in liste)
                {
                    Anim celle = a;
                    var element = new MenuItem { Header = a.Nom };
                    element.Click += (o, e) => { JouerDemande(celle); Dire(celle.Nom, 2.5); };
                    sous.Items.Add(element);
                }
                animations.Items.Add(sous);
            }
            menu.Items.Add(animations);

            var fenetre = new MenuItem { Header = "Sauter sur une fenêtre" };
            fenetre.Click += (o, e) => { if (etat == Etat.Anime && !SauterSurFenetre()) Dire("Aucune fenêtre où sauter", 2.5); };
            menu.Items.Add(fenetre);
            var teleport = new MenuItem { Header = "Se téléporter" };
            teleport.Click += (o, e) => Teleporter();
            menu.Items.Add(teleport);
            var coin = new MenuItem { Header = "Revenir dans le coin" };
            coin.Click += (o, e) =>
            {
                if (etat != Etat.Anime) return;
                Rect zone = SystemParameters.WorkArea;
                R.Mettre("droite", 1120);
                maison = new Point(Math.Max(SystemParameters.VirtualScreenLeft + 60, zone.Right - 1120), zone.Bottom);
                ancre = maison; support = IntPtr.Zero;
                Repos();
            };
            menu.Items.Add(coin);
            var demarrage = new MenuItem { Header = "Lancer au démarrage de Windows", IsCheckable = true, IsChecked = AuDemarrage() };
            demarrage.Click += (o, e) => DefinirDemarrage(demarrage.IsChecked);
            menu.Items.Add(demarrage);
            menu.Items.Add(new Separator());
            var quitter = new MenuItem { Header = "Quitter" };
            quitter.Click += (o, e) => Close();
            menu.Items.Add(quitter);
            toile.ContextMenu = menu;
        }

        public void OuvrirReglages()
        {
            if (fenetreReglages != null && fenetreReglages.IsVisible) { fenetreReglages.Activate(); return; }
            fenetreReglages = new Parametres(this);
            fenetreReglages.Show();
        }

        const string CleDemarrage = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static bool AuDemarrage()
        {
            using (RegistryKey cle = Registry.CurrentUser.OpenSubKey(CleDemarrage))
                return cle != null && cle.GetValue("MascotteStickman") != null;
        }

        static void DefinirDemarrage(bool actif)
        {
            using (RegistryKey cle = Registry.CurrentUser.OpenSubKey(CleDemarrage, true))
            {
                if (cle == null) return;
                if (actif) cle.SetValue("MascotteStickman", "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"");
                else cle.DeleteValue("MascotteStickman", false);
            }
        }

        // ------------------------------------------------------------ fenêtres-plateformes
        // Le haut d'une fenêtre visible sert de sol : il y saute, s'y promène, voyage avec elle,
        // et retombe si elle disparaît ou passe derrière une autre.

        struct Bord
        {
            public IntPtr Fenetre;
            public double Gauche, Droite, Haut;
        }

        double HauteurCorps { get { return (2 * R.D("jambes") + R.D("torse") + 2 * R.D("tete") + 14) * s; } }

        bool LireBord(IntPtr fenetre, out Bord bord)
        {
            bord = new Bord();
            RECT r;
            int voile;
            if (!IsWindow(fenetre) || !IsWindowVisible(fenetre) || IsIconic(fenetre)) return false;
            if (DwmGetWindowAttribute(fenetre, DWMWA_CLOAKED, out voile, 4) == 0 && voile != 0) return false;
            if (DwmGetWindowAttribute(fenetre, DWMWA_EXTENDED_FRAME_BOUNDS, out r, 16) != 0) return false;
            PresentationSource source = PresentationSource.FromVisual(this);
            Matrix m = source != null ? source.CompositionTarget.TransformFromDevice : Matrix.Identity;
            Point hautGauche = m.Transform(new Point(r.Left, r.Top)), basDroite = m.Transform(new Point(r.Right, r.Bottom));
            if (basDroite.X - hautGauche.X < 200 || basDroite.Y - hautGauche.Y < 80) return false;
            if (hautGauche.Y < SystemParameters.VirtualScreenTop + HauteurCorps) return false;       // pas la place de se tenir dessus
            bord.Fenetre = fenetre; bord.Gauche = hautGauche.X; bord.Droite = basDroite.X; bord.Haut = hautGauche.Y;
            return true;
        }

        List<Bord> Bords()
        {
            var bords = new List<Bord>();
            uint moi = (uint)Process.GetCurrentProcess().Id;
            EnumWindows((fenetre, parametre) =>
            {
                uint processus;
                Bord bord;
                GetWindowThreadProcessId(fenetre, out processus);
                int style = GetWindowLong(fenetre, GWL_EXSTYLE);
                if (processus != moi && (style & (WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE)) == 0
                    && GetWindowTextLength(fenetre) > 0 && LireBord(fenetre, out bord))
                    bords.Add(bord);
                return true;
            }, IntPtr.Zero);
            return bords;
        }

        bool BordLibre(Bord bord, double x)
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            Point pixel = (source != null ? source.CompositionTarget.TransformToDevice : Matrix.Identity).Transform(new Point(x, bord.Haut + 3));
            POINT p;
            p.X = (int)pixel.X; p.Y = (int)pixel.Y;
            IntPtr dessus = GetAncestor(WindowFromPoint(p), GA_ROOT);
            return dessus == bord.Fenetre || dessus == poignee;
        }

        // Un endroit libre sur le haut d'une autre fenêtre, de préférence proche.
        bool ChercherBord(out Point arrivee, out IntPtr fenetre)
        {
            double sol = SystemParameters.WorkArea.Bottom, meilleur = double.MaxValue;
            arrivee = new Point();
            fenetre = IntPtr.Zero;
            foreach (Bord bord in Bords())
            {
                if (bord.Fenetre == support || bord.Haut > sol - 60) continue;
                double gauche = bord.Gauche + 30, droite = bord.Droite - 30;
                for (int essai = 0; essai < 5; essai++)
                {
                    double x = essai == 0 ? Math.Max(gauche, Math.Min(droite, ancre.X)) : gauche + hasard.NextDouble() * (droite - gauche);
                    if (!BordLibre(bord, x)) continue;
                    double distance = (Math.Abs(x - ancre.X) + 100) * (0.5 + hasard.NextDouble());
                    if (distance < meilleur) { meilleur = distance; arrivee = new Point(x, bord.Haut); fenetre = bord.Fenetre; }
                    break;
                }
            }
            return fenetre != IntPtr.Zero;
        }

        // Disparaît en fondu et réapparaît ailleurs : sur une fenêtre, ou plus loin sur le sol.
        bool Teleporter()
        {
            if (etat != Etat.Anime) return false;
            Point but;
            IntPtr fenetre;
            if (!ChercherBord(out but, out fenetre) || hasard.Next(3) == 0)
            {
                double gauche = SystemParameters.VirtualScreenLeft + 80 * s, droite = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80 * s;
                but = new Point(gauche + hasard.NextDouble() * (droite - gauche), SystemParameters.WorkArea.Bottom);
                fenetre = IntPtr.Zero;
            }
            Point ou = but;
            IntPtr sur = fenetre;
            Sons.Jouer("pop", "sonsPouvoirs");
            voileVise = 0;
            Jouer(Biblio.Concentration, 1, () =>
            {
                ancre = ou;
                support = sur;
                Bord bord;
                if (support != IntPtr.Zero && LireBord(support, out bord)) supportX = ancre.X - bord.Gauche;
                else { support = IntPtr.Zero; ancre.Y = SystemParameters.WorkArea.Bottom; }
                voileVise = 1;
                Sons.Jouer("pop", "sonsPouvoirs");
                Jouer(Biblio.Apparition, 1);
            });
            return true;
        }

        bool SauterSurFenetre()
        {
            double sol = SystemParameters.WorkArea.Bottom;
            Point arrivee;
            IntPtr vers;
            if (!ChercherBord(out arrivee, out vers))
            {
                if (support == IntPtr.Zero) return false;
                arrivee = new Point(ancre.X + (hasard.Next(2) == 0 ? -1 : 1) * (60 + hasard.NextDouble() * 160), sol);       // sinon il redescend
            }
            Point but = arrivee;
            if (Math.Abs(but.X - ancre.X) > 1100 && support == IntPtr.Zero)
            {
                AllerVers(but.X - Math.Sign(but.X - ancre.X) * 450, Biblio.Course, () => Bondir(but));    // trop loin : il court d'abord
                return true;
            }
            Bondir(but);
            return true;
        }

        // Saut calculé pour retomber exactement au point visé.
        void Bondir(Point arrivee)
        {
            double g = 2300 * s * R.D("gravite") / 100;
            double dx = arrivee.X - ancre.X, dy = arrivee.Y - ancre.Y;
            double duree = Math.Max(0.5, Math.Min(1.3, 0.45 + (Math.Abs(dx) + Math.Abs(dy)) / 1500)) * Math.Sqrt(100 / R.D("gravite"));
            if (dy < 0) duree = Math.Max(duree, Math.Sqrt(-2 * dy / g) * 1.35);                 // assez long pour arriver par le dessus
            if (dx != 0) face = dx > 0 ? 1 : -1;
            int reglage = (int)R.D("styleSaut");
            styleSaut = reglage == 0 ? hasard.Next(3) : reglage - 1;
            tVol = 0;
            dureeVol = duree;
            Sons.Jouer(Math.Abs(dx) + Math.Abs(dy) > 700 ? "grandsaut" : "saut", "sonsSauts");
            Lancer(new Vector(dx / duree, dy / duree - 0.5 * g * duree), true, 0);
        }

        bool SupportPerdu()
        {
            if (support == IntPtr.Zero) return false;
            Bord bord;
            double x = 0;
            bool ok = LireBord(support, out bord);
            if (ok) { x = bord.Gauche + supportX; ok = x > bord.Gauche + 4 && x < bord.Droite - 4 && BordLibre(bord, x); }
            if (!ok)                                                                                 // plus rien sous les pieds : il tombe
            {
                styleSaut = 0;
                Sons.Jouer("glisse", "sonsSauts");
                Lancer(new Vector(0, 0), true, 0);
                return true;
            }
            ancre = new Point(x, bord.Haut);                                                         // la fenêtre a bougé : il voyage avec
            return false;
        }

        static double SecondesSansSaisie()
        {
            var info = new LASTINPUTINFO { Taille = 8 };
            return GetLastInputInfo(ref info) ? unchecked((uint)Environment.TickCount - info.Dernier) / 1000.0 : 0;
        }

        // ------------------------------------------------------------ Win32

        const int GWL_EXSTYLE = -20, WS_EX_TOOLWINDOW = 0x80, WS_EX_TRANSPARENT = 0x20, WS_EX_NOACTIVATE = 0x08000000;
        const int DWMWA_EXTENDED_FRAME_BOUNDS = 9, DWMWA_CLOAKED = 14;
        const uint GA_ROOT = 2;

        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint Taille, Dernier; }
        delegate bool RappelFenetre(IntPtr fenetre, IntPtr parametre);

        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr fenetre, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr fenetre, int index, int valeur);
        [DllImport("user32.dll")] static extern bool EnumWindows(RappelFenetre rappel, IntPtr parametre);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr fenetre);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr fenetre);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr fenetre);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr fenetre);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr fenetre, out uint processus);
        [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT point);
        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr fenetre, uint drapeau);
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr fenetre, int attribut, out RECT valeur, int taille);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr fenetre, int attribut, out int valeur, int taille);
    }

    // ============================================================ fenêtre des réglages
    // Fabriquée à partir de la liste des réglages : un onglet par catégorie, une ligne par réglage,
    // plus un onglet qui liste toutes les animations (à cocher, à essayer).

    sealed class Parametres : Window
    {
        readonly Bonhomme bonhomme;
        ListBox liste;
        TextBox recherche;
        TextBlock compte;

        public Parametres(Bonhomme proprietaire)
        {
            bonhomme = proprietaire;
            int reglages = R.Tous.Count(p => p.Cat != "");
            Title = "Stickman — " + Biblio.Toutes.Count + " animations, " + (reglages + Biblio.Toutes.Count) + " réglages";
            Width = 640; Height = 700;
            Topmost = true;                                      // reste visible pendant qu'on règle : on voit l'effet en direct
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var onglets = new TabControl { Margin = new Thickness(6) };
            foreach (string categorie in R.Tous.Select(p => p.Cat).Where(c => c != "").Distinct())
            {
                var pile = new StackPanel { Margin = new Thickness(12) };
                if (categorie == "Familles") pile.Children.Add(new TextBlock { Text = "À quelle fréquence il choisit chaque famille d'animations (0 = jamais).", Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
                foreach (Param p in R.Tous.Where(x => x.Cat == categorie)) pile.Children.Add(Ligne(p));
                var remise = new Button { Content = "Remettre cet onglet à zéro", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 14, 0, 0) };
                string cat = categorie;
                remise.Click += (o, e) =>
                {
                    foreach (Param p in R.Tous.Where(x => x.Cat == cat)) R.Mettre(p.Cle, p.Defaut);
                    bonhomme.Appliquer();
                    Close();
                    bonhomme.OuvrirReglages();               // rouverte pour que les curseurs reprennent leurs valeurs
                };
                pile.Children.Add(remise);
                onglets.Items.Add(new TabItem { Header = categorie, Content = new ScrollViewer { Content = pile, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
            }
            onglets.Items.Add(new TabItem { Header = "Animations (" + Biblio.Toutes.Count + ")", Content = OngletAnimations() });
            Content = onglets;
        }

        UIElement Ligne(Param p)
        {
            var grille = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grille.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            grille.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grille.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            var nom = new TextBlock { Text = p.Nom, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            grille.Children.Add(nom);
            UIElement controle;
            if (p.Type == 'b')
            {
                var coche = new CheckBox { IsChecked = p.V != 0, VerticalAlignment = VerticalAlignment.Center };
                coche.Click += (o, e) => { R.Mettre(p.Cle, coche.IsChecked == true ? 1 : 0); bonhomme.Appliquer(); };
                controle = coche;
            }
            else if (p.Type == 'c')
            {
                var choix = new ComboBox { ItemsSource = p.Choix, SelectedIndex = (int)p.V };
                choix.SelectionChanged += (o, e) => R.Mettre(p.Cle, choix.SelectedIndex);
                controle = choix;
            }
            else if (p.Type == 'k') controle = Nuancier(p);
            else
            {
                var valeur = new TextBlock { Text = Ecrire(p), VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
                Grid.SetColumn(valeur, 2);
                grille.Children.Add(valeur);
                var curseur = new Slider { Minimum = p.Min, Maximum = p.Max, Value = p.V, VerticalAlignment = VerticalAlignment.Center };
                curseur.ValueChanged += (o, e) => { R.Mettre(p.Cle, p.Max - p.Min > 20 ? Math.Round(curseur.Value) : Math.Round(curseur.Value, 2)); valeur.Text = Ecrire(p); bonhomme.Appliquer(); };
                controle = curseur;
            }
            Grid.SetColumn(controle, 1);
            grille.Children.Add(controle);
            return grille;
        }

        static string Ecrire(Param p) { return p.V.ToString(p.Max - p.Min > 20 ? "0" : "0.##", CultureInfo.CurrentCulture); }

        // Couleur : les teintes toutes prêtes, puis trois curseurs pour n'importe quelle autre.
        UIElement Nuancier(Param p)
        {
            var pile = new StackPanel();
            var pastilles = new WrapPanel();
            var curseurs = new Slider[3];
            var apercu = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Background = new SolidColorBrush(R.Couleur(p.Cle)), Margin = new Thickness(0, 0, 10, 0) };
            Action<int> choisir = rvb =>
            {
                R.Mettre(p.Cle, rvb);
                if (p.Cle == "couleur") R.Mettre("arcenciel", 0);
                apercu.Background = new SolidColorBrush(R.Rvb(rvb));
                bonhomme.Appliquer();
            };
            foreach (Bonhomme.Nuance teinte in Bonhomme.Palette)
            {
                int rvb = teinte.Rvb;
                var pastille = new Button { Width = 24, Height = 24, Margin = new Thickness(2), Background = new SolidColorBrush(R.Rvb(rvb)), ToolTip = teinte.Nom + (teinte.Creuse ? " (tête creuse)" : " (tête pleine)") };
                pastille.Click += (o, e) => { choisir(rvb); for (int i = 0; i < 3; i++) curseurs[i].Value = (rvb >> (16 - 8 * i)) & 255; };
                pastilles.Children.Add(pastille);
            }
            pile.Children.Add(pastilles);
            var ligne = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            ligne.Children.Add(apercu);
            var trois = new StackPanel { Width = 190 };
            for (int i = 0; i < 3; i++)
            {
                curseurs[i] = new Slider { Minimum = 0, Maximum = 255, Value = ((int)p.V >> (16 - 8 * i)) & 255, Foreground = i == 0 ? Brushes.Red : i == 1 ? Brushes.Green : Brushes.Blue, ToolTip = i == 0 ? "Rouge" : i == 1 ? "Vert" : "Bleu" };
                curseurs[i].ValueChanged += (o, e) => choisir(((int)curseurs[0].Value << 16) | ((int)curseurs[1].Value << 8) | (int)curseurs[2].Value);
                trois.Children.Add(curseurs[i]);
            }
            ligne.Children.Add(trois);
            pile.Children.Add(ligne);
            return pile;
        }

        UIElement OngletAnimations()
        {
            var panneau = new DockPanel { Margin = new Thickness(10) };
            var haut = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            recherche = new TextBox { Width = 190, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Chercher par nom ou par famille" };
            recherche.TextChanged += (o, e) => Remplir();
            haut.Children.Add(new TextBlock { Text = "Chercher : ", VerticalAlignment = VerticalAlignment.Center });
            haut.Children.Add(recherche);
            haut.Children.Add(Bouton("▶ Jouer", () => { var c = liste.SelectedItem as CheckBox; if (c != null) Essayer((Anim)c.Tag); }));
            haut.Children.Add(Bouton("Tout cocher", () => Cocher(true)));
            haut.Children.Add(Bouton("Tout décocher", () => Cocher(false)));
            DockPanel.SetDock(haut, Dock.Top);
            panneau.Children.Add(haut);
            compte = new TextBlock { Margin = new Thickness(0, 6, 0, 0), Foreground = Brushes.Gray };
            DockPanel.SetDock(compte, Dock.Bottom);
            panneau.Children.Add(compte);
            liste = new ListBox();
            liste.MouseDoubleClick += (o, e) => { var c = liste.SelectedItem as CheckBox; if (c != null) Essayer((Anim)c.Tag); };
            panneau.Children.Add(liste);
            Remplir();
            return panneau;
        }

        Button Bouton(string texte, Action action)
        {
            var bouton = new Button { Content = texte, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(10, 3, 10, 3) };
            bouton.Click += (o, e) => action();
            return bouton;
        }

        void Essayer(Anim a)
        {
            bonhomme.Jouer(a, Math.Max(1, a.Tours));
            bonhomme.Dire(a.Nom, 2.5);
        }

        void Remplir()
        {
            liste.Items.Clear();
            string filtre = recherche.Text.Trim();
            foreach (Anim a in Biblio.Toutes)
            {
                if (filtre != "" && (a.Famille + " " + a.Nom).IndexOf(filtre, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                Anim celle = a;
                var coche = new CheckBox { Content = a.Famille + "  —  " + a.Nom, IsChecked = !R.Coupees.Contains(a.Nom), Tag = a, Margin = new Thickness(2) };
                coche.Click += (o, e) =>
                {
                    if (coche.IsChecked == true) R.Coupees.Remove(celle.Nom); else R.Coupees.Add(celle.Nom);
                    R.Sale = true;
                    Compter();
                };
                liste.Items.Add(coche);
            }
            Compter();
        }

        void Cocher(bool oui)
        {
            foreach (CheckBox coche in liste.Items)
            {
                coche.IsChecked = oui;
                string nom = ((Anim)coche.Tag).Nom;
                if (oui) R.Coupees.Remove(nom); else R.Coupees.Add(nom);
            }
            R.Sale = true;
            Compter();
        }

        void Compter()
        {
            compte.Text = liste.Items.Count + " affichées — " + (Biblio.Toutes.Count - R.Coupees.Count) + " animations actives sur " + Biblio.Toutes.Count
                + ". Double-clic pour en essayer une ; décochée, elle n'est plus choisie au hasard.";
        }
    }
}
