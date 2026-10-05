// Bibliothèque d'animations du stickman.
// Une pose = 13 nombres (voir I) ; une animation = une fonction « phase 0..1 -> pose ».
// Les poses s'écrivent en texte, dans cet ordre :
//   torse tête | épaule1 coude1 épaule2 coude2 | hanche1 genou1 hanche2 genou2 | hauteur rotation décalage
// Angles en degrés, personnage tourné vers la droite : 0 = membre pendant vers le bas, 90 = vers l'avant,
// 180 = vers le haut. Un genou plié est négatif. Six nombres seulement = geste du haut du corps.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace MascotteStickman
{
    static class I
    {
        public const int X = 0, Air = 1, Rot = 2, Torse = 3, Tete = 4, Ep1 = 5, Co1 = 6, Ep2 = 7, Co2 = 8,
            Ha1 = 9, Ge1 = 10, Ha2 = 11, Ge2 = 12, N = 13;
    }

    sealed class Anim
    {
        public string Nom, Famille, Objet;
        public double Duree = 1;                 // secondes par cycle
        public int Tours = 1;                    // cycles quand elle est tirée au hasard
        public bool Haut;                        // haut du corps seulement : se superpose à ce que font les jambes
        public bool Deplace;                     // déplacement : jouée jusqu'à destination
        public double Vitesse;                   // px/s vers l'avant (négatif : à reculons)
        public string Son;                       // bruitage joué une fois, quand l'animation atteint SonA
        public double SonA;                      // en fraction du premier cycle
        public bool SurFenetre;                  // n'a de sens que perché sur une fenêtre (jambes dans le vide…)
        public Func<double, double[]> Pose;
    }

    static class Biblio
    {
        public static readonly List<Anim> Toutes = new List<Anim>();
        public static readonly List<string> Familles = new List<string>();
        public static readonly List<Anim> Repos = new List<Anim>();
        public static Anim Marche, Course, Reception, Heros, SeReleve, Dort, Salut, PoingHaut, Poing, PiedMoyen, PiedBas, Danse, Concentration, Apparition;
        public static double[] Groupe;
        public static double[] SautMonte, SautDescend;

        const string S = "0 0 10 12 -10 12 6 0 -6 0";                       // debout
        const string Cr = "22 5 -30 25 -40 25 62 -112 52 -104";             // accroupi
        const string G = "8 0 70 100 55 110 14 -15 -14 -10";                // en garde
        const string Boule = "20 20 60 80 55 85 105 -130 98 -125";          // groupé (saltos)
        const string Dos = "0 10 25 5 28 -5 15 0 12 0 0 -75";               // allongé sur le dos
        const string Ventre = "0 0 165 20 170 10 -15 0 -12 0 0 75";         // allongé sur le ventre
        const string Planche = "0 -10 62 0 64 0 0 0 2 0 0 62";              // en appui sur les mains

        static readonly int[] Ordre = { I.Torse, I.Tete, I.Ep1, I.Co1, I.Ep2, I.Co2, I.Ha1, I.Ge1, I.Ha2, I.Ge2, I.Air, I.Rot, I.X };

        // ------------------------------------------------------------ outils

        public static double[] Neutre() { return L(S); }

        public static double[] L(string texte)
        {
            var p = new double[I.N];
            p[I.Ha1] = 6; p[I.Ha2] = -6;
            string[] m = texte.Split(new[] { ' ', '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < m.Length; i++) p[Ordre[i]] = double.Parse(m[i], CultureInfo.InvariantCulture);
            return p;
        }

        static int Nombres(string texte) { return texte.Split(new[] { ' ', '|' }, StringSplitOptions.RemoveEmptyEntries).Length; }

        public static double[] Mix(double[] a, double[] b, double k)
        {
            var p = new double[I.N];
            for (int i = 0; i < I.N; i++) p[i] = a[i] + (b[i] - a[i]) * k;
            return p;
        }

        static double[] Echange(double[] p)
        {
            var q = (double[])p.Clone();
            q[I.Ep1] = p[I.Ep2]; q[I.Co1] = p[I.Co2]; q[I.Ep2] = p[I.Ep1]; q[I.Co2] = p[I.Co1];
            q[I.Ha1] = p[I.Ha2]; q[I.Ge1] = p[I.Ge2]; q[I.Ha2] = p[I.Ha1]; q[I.Ge2] = p[I.Ge1];
            return q;
        }

        public static Anim Trouver(string nom)
        {
            foreach (Anim a in Toutes) if (string.Equals(a.Nom, nom, StringComparison.OrdinalIgnoreCase)) return a;
            foreach (Anim a in Toutes) if (a.Nom.StartsWith(nom, StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        // famille null = animation interne (réception, se relever…) : ni tirée au hasard ni listée
        static Anim Ajouter(string famille, string nom, double duree, int tours, Func<double, double[]> pose)
        {
            var a = new Anim { Nom = nom, Famille = famille, Duree = duree, Tours = tours, Pose = pose };
            if (famille == null) return a;
            Toutes.Add(a);
            if (!Familles.Contains(famille)) Familles.Add(famille);
            return a;
        }

        // Va-et-vient régulier entre deux poses.
        static Anim Osc(string famille, string nom, string a, string b, double periode, int tours, string objet = null)
        {
            double[] pa = L(a), pb = L(b);
            Anim x = Ajouter(famille, nom, periode, tours, t => Mix(pa, pb, (1 - Math.Cos(2 * Math.PI * t)) / 2));
            x.Haut = Nombres(a) <= 6;
            x.Objet = objet;
            return x;
        }

        // Pose tenue, avec une légère respiration.
        static Anim Fixe(string famille, string nom, string a, int tours = 3, string objet = null)
        {
            double[] pa = L(a), pb = L(a);
            pb[I.Torse] += 2; pb[I.Tete] += 2;
            Anim x = Ajouter(famille, nom, 1.6, tours, t => Mix(pa, pb, (1 - Math.Cos(2 * Math.PI * t)) / 2));
            x.Haut = Nombres(a) <= 6;
            x.Objet = objet;
            return x;
        }

        // Suite de poses datées : "ms:pose;ms:pose;…", reliées par une courbe lisse.
        static Anim Cles(string famille, string nom, string texte, string objet = null, double vitesse = 0)
        {
            string[] morceaux = texte.Split(';');
            int n = morceaux.Length;
            var temps = new double[n];
            var cles = new double[n][];
            for (int i = 0; i < n; i++)
            {
                int deux = morceaux[i].IndexOf(':');
                temps[i] = double.Parse(morceaux[i].Substring(0, deux), CultureInfo.InvariantCulture) / 1000;
                cles[i] = L(morceaux[i].Substring(deux + 1));
            }
            return ClesP(famille, nom, temps, cles, objet, vitesse);
        }

        static Anim ClesP(string famille, string nom, double[] temps, double[][] cles, string objet = null, double vitesse = 0)
        {
            double total = temps[temps.Length - 1];
            Anim x = Ajouter(famille, nom, total, 1, t => Courbe(temps, cles, t * total));
            x.Objet = objet;
            x.Vitesse = vitesse;
            return x;
        }

        static double[] Courbe(double[] temps, double[][] cles, double t)
        {
            int n = cles.Length, i = 0;
            while (i < n - 2 && t > temps[i + 1]) i++;
            double u = Math.Max(0, Math.Min(1, (t - temps[i]) / (temps[i + 1] - temps[i])));
            double[] p0 = cles[Math.Max(i - 1, 0)], p1 = cles[i], p2 = cles[i + 1], p3 = cles[Math.Min(i + 2, n - 1)];
            var p = new double[I.N];
            for (int k = 0; k < I.N; k++)
                p[k] = 0.5 * (2 * p1[k] + (p2[k] - p0[k]) * u + (2 * p0[k] - 5 * p1[k] + 4 * p2[k] - p3[k]) * u * u
                              + (3 * p1[k] - p0[k] - 3 * p2[k] + p3[k]) * u * u * u);
            p[I.Ge1] = Math.Min(p[I.Ge1], 0);
            p[I.Ge2] = Math.Min(p[I.Ge2], 0);
            return p;
        }

        // La même animation de l'autre main (et de l'autre jambe).
        static Anim Autre(Anim a, string suffixe = " (autre main)")
        {
            Anim b = Ajouter(a.Famille, a.Nom + suffixe, a.Duree, a.Tours, t => Echange(a.Pose(t)));
            b.Haut = a.Haut; b.Objet = a.Objet; b.Vitesse = a.Vitesse; b.Deplace = a.Deplace;
            return b;
        }

        public static void Construire()
        {
            SautMonte = L("-4 -10 150 20 -160 -10 30 -50 -10 -30");
            SautDescend = L("6 5 120 40 -120 -40 20 -30 -15 -40");
            Reception = Cles(null, "Réception", "0:" + Cr + ";180:" + Cr + ";420:" + S);
            SeReleve = Cles(null, "Se relève", "0:" + Ventre + ";500:" + Ventre + ";800:" + Planche + ";1050:" + Cr + ";1350:" + S);
            Groupe = L(Boule);
            const string troisPoints = "32 -22 20 0 -105 -15 70 -120 -10 -100";
            Heros = Cles(null, "Atterrissage de héros", "0:" + troisPoints + ";650:" + troisPoints + ";1000:" + S);
            Concentration = Cles(null, "Disparaît", "0:" + S + ";250:" + Cr + ";450:" + Cr);
            Apparition = Cles(null, "Apparaît", "0:" + Cr + ";150:" + Cr + ";450:" + S);

            Attentes();
            Deplacements();
            Danses();
            Gestes();
            Combat();
            Acrobaties();
            Sport();
            Quotidien();
            Emotions();
            Nouveautes();
            Sonoriser();
        }

        // ------------------------------------------------------------ deuxième fournée : fenêtres, pouvoirs, numéros

        static void Nouveautes()
        {
            // sur une fenêtre : les jambes pendent dans le vide (hauteur négative = sous la ligne du sol)
            Anim bord = Osc("Fenêtres", "Assis au bord, balance les jambes", "-8 0 -25 -10 -35 -10 22 -4 4 -32 -44", "-8 3 -25 -10 -35 -10 4 -32 22 -4 -44", 1.0, 8);
            bord.SurFenetre = true;
            Anim reve = Osc("Fenêtres", "Assis au bord, rêvasse", "-14 -18 -30 -12 -40 -12 14 -8 8 -14 -44", "-16 -22 -30 -12 -40 -12 12 -10 10 -12 -44", 2.4, 4, "note");
            reve.SurFenetre = true;
            Anim guette = Osc("Fenêtres", "Regarde en bas", "55 25 30 5 20 10 75 -120 60 -110", "62 30 34 5 24 10 78 -124 62 -112", 1.6, 3);
            guette.SurFenetre = true;
            Anim allonge = Osc("Fenêtres", "Allongé au bord, un bras dans le vide", "0 5 -72 0 28 -5 15 0 12 0 -36 -75", "0 8 -58 0 28 -5 15 0 12 0 -34 -75", 2.2, 4);
            allonge.SurFenetre = true;
            Anim funambule = Pas("Funambule", 18, 22, 0, 0, 0, 0, 30, 1.3, 6);
            funambule.Famille = "Fenêtres";
            if (!Familles.Contains("Fenêtres")) Familles.Add("Fenêtres");

            // d'autres façons d'avancer
            Anim rampe = Ajouter("Déplacements", "Rampe", 1.1, 1, t =>
            {
                double s = Math.Sin(2 * Math.PI * t);
                double[] p = L("0 -25 0 0 0 0 0 0 0 0 0 78");
                p[I.Ep1] = 150 + 30 * s; p[I.Co1] = 40 - 30 * s; p[I.Ep2] = 150 - 30 * s; p[I.Co2] = 40 + 30 * s;
                p[I.Ha1] = -10 + 30 * Math.Max(0, s); p[I.Ge1] = -70 * Math.Max(0, s); p[I.Ha2] = -10 + 30 * Math.Max(0, -s); p[I.Ge2] = -70 * Math.Max(0, -s);
                return p;
            });
            rampe.Deplace = true; rampe.Vitesse = 28;
            Anim roule = Ajouter("Déplacements", "Roule en boule", 0.55, 1, t => { double[] p = L(Boule); p[I.Rot] = 360 * t; return p; });
            roule.Deplace = true; roule.Vitesse = 170; roule.Son = "swish";
            Anim grenouille = Ajouter("Déplacements", "Sauts de grenouille", 0.8, 1, t =>
            {
                double u = Math.Max(0, Math.Sin(2 * Math.PI * t));
                double[] p = L(Cr);
                p[I.Air] = 34 * u; p[I.Ha1] = 62 - 40 * u; p[I.Ge1] = -112 + 90 * u; p[I.Ha2] = 52 - 60 * u; p[I.Ge2] = -104 + 80 * u; p[I.Ep1] = -30 + 150 * u; p[I.Ep2] = -40 + 150 * u;
                return p;
            });
            grenouille.Deplace = true; grenouille.Vitesse = 95; grenouille.Son = "saut";
            Anim patine = Pas("Patinage", 34, 12, 40, 10, 14, 0, 150, 1.5);
            patine.Objet = "planche";

            // pouvoirs et armes
            const string F = "Combat";
            Cles(F, "Rayon d'énergie", "0:" + G + ";400:-12 0 -35 70 -25 80 20 -30 -18 -25;800:-14 0 -38 72 -28 82 22 -34 -18 -28;900:18 0 92 2 86 6 28 -26 -26 -6 0 0 10;1700:20 0 93 2 87 6 30 -28 -26 -6 0 0 8;2000:" + G, "rayon");
            Cles(F, "Tir à l'arc", "0:" + S + ";300:-4 0 20 120 92 2 14 -6 -16 -4;700:-8 0 -30 150 92 2 16 -8 -16 -4;800:-6 0 10 100 92 2 16 -8 -16 -4;1200:-6 0 10 100 92 2 16 -8 -16 -4;1500:" + S, "arc");
            Cles(F, "Lance un shuriken", "0:" + G + ";200:-10 -5 -100 70 60 60 -10 -12 16 -10;320:16 0 95 5 -40 40 28 -24 -22 -6 0 0 8;700:16 0 60 10 -40 40 28 -24 -22 -6 0 0 8;950:" + G, "shuriken");
            Osc(F, "Bouclier", "8 0 75 95 -20 60 20 -22 -16 -14", "10 2 78 92 -22 62 22 -26 -16 -16", 0.7, 4, "bouclier");
            Ajouter(F, "Toupie de combat", 0.45, 5, t => { double[] p = L("0 0 90 0 -90 0 60 -30 -20 -10"); p[I.Ep1] = 90 + 360 * t; p[I.Ep2] = -90 + 360 * t; p[I.Air] = 6; return p; }).Objet = "baton";

            // numéros
            const string A = "Acrobaties";
            Cles(A, "Double saut", "0:" + S + ";150:" + Cr + ";300:-4 -10 172 5 -172 -5 5 0 -5 0 34;440:" + Boule + " 42;560:-4 -10 150 20 -150 -20 20 -40 -10 -30 60;700:-4 -10 172 5 -172 -5 5 0 -5 0 92;860:8 0 60 30 50 30 30 -50 20 -45 50;1000:" + Cr + ";1200:" + S);
            Ajouter(A, "Moulin à vent", 0.7, 5, t => { double[] p = L("0 0 140 20 -140 -20 50 -10 -50 -10"); p[I.Rot] = 360 * t; return p; }).Son = "swish";
            Ajouter(A, "Le ver", 1.0, 4, t =>
            {
                double f = 2 * Math.PI * t;
                double[] p = L("0 0 150 60 152 58 -15 0 -12 0 0 78");
                p[I.Torse] = -18 * Math.Sin(f); p[I.Ha1] = -15 + 22 * Math.Sin(f + 1.5); p[I.Ha2] = -12 + 22 * Math.Sin(f + 1.5); p[I.Air] = 5 * Math.Max(0, Math.Sin(f + 0.8));
                return p;
            }).Vitesse = 30;
            Cles(A, "Glissade sur les genoux", "0:" + S + ";200:10 0 60 30 50 30 40 -60 20 -50;420:-25 -20 160 -10 -160 10 10 -100 0 -95;1100:-28 -22 165 -6 -165 6 10 -100 0 -95;1500:" + S, "note", 190);
            Ajouter("Quotidien", "Jongle", 0.6, 8, t =>
            {
                double s = Math.Sin(2 * Math.PI * t);
                double[] p = L("0 -12 0 0 0 0 8 0 -8 0");
                p[I.Ep1] = 40 + 12 * s; p[I.Co1] = 80 - 14 * s; p[I.Ep2] = 30 - 12 * s; p[I.Co2] = 85 + 14 * s;
                return p;
            }).Objet = "jongle";
            Osc("Quotidien", "Joue au yo-yo", "4 12 70 20 -10 12 8 0 -8 0", "4 14 60 40 -10 12 8 0 -8 0", 0.7, 8, "yoyo");
            Osc("Quotidien", "Prend un selfie", "-4 -8 115 15 -35 105 10 0 -8 0", "-6 -10 118 12 -35 105 14 -6 -8 0", 1.0, 3, "telephone");
            Osc("Émotions", "Cœur avec les bras", "0 -8 160 70 -160 -70 8 0 -8 0", "0 -10 163 74 -163 -74 8 0 -8 0 3", 0.8, 4, "coeur");
            Cles("Émotions", "Saute de joie en tournant", "0:" + S + ";150:" + Cr + ";300:-4 -10 172 5 -172 -5 5 0 -5 0 30;430:" + Boule + " 52 180;560:-4 -10 172 5 -172 -5 5 0 -5 0 30 345;680:" + Cr + " 0 360;850:-6 -15 160 -10 -160 10 8 0 -8 0 0 360;1300:-8 -18 165 -5 -165 5 8 0 -8 0 0 360;1500:" + S + " 0 360", "note");
        }

        // Les bruitages : attribués d'après la famille et le nom, pour ne pas les répéter sur 300 lignes.
        static void Sonoriser()
        {
            foreach (Anim a in Toutes)
            {
                if (a.Son != null) continue;
                string n = a.Nom;
                switch (a.Famille)
                {
                    case "Combat":
                        if (n.StartsWith("Épée") || n.StartsWith("Bâton") || n.StartsWith("Lance") || n.StartsWith("Toupie")) Bruit(a, "swish", 0.3);
                        else if (n.Contains("énergie") || n.StartsWith("Onde") || n.StartsWith("Rayon")) Bruit(a, "energie", 0.05);
                        else if (n.StartsWith("Tir")) Bruit(a, "swish", 0.5);
                        else if (!n.StartsWith("Garde") && !n.StartsWith("Sautille") && !n.StartsWith("Provocation") && !n.StartsWith("Salut") && !n.StartsWith("Esquive") && !n.StartsWith("Bouclier")) Bruit(a, "coup", 0.4);
                        break;
                    case "Acrobaties":
                        if (n.StartsWith("Roulade") || n.StartsWith("Roue") || n.StartsWith("Toupie")) Bruit(a, "swish", 0.2);
                        else if (n.StartsWith("Sa") || n.StartsWith("Double") || n.StartsWith("Flip") || n.StartsWith("Kip") || n.StartsWith("Plongeon")) Bruit(a, "saut", 0.15);
                        break;
                    case "Émotions":
                        if (n.StartsWith("Victoire") || n.StartsWith("Joie") || n.StartsWith("Danse") || n.StartsWith("Saute")) Bruit(a, "tada", 0.1);
                        else if (n.StartsWith("Surprise")) Bruit(a, "pop", 0.05);
                        break;
                    case "Quotidien":
                        if (n.StartsWith("Glisse") || n.StartsWith("Trébuche")) Bruit(a, "glisse", 0.05);
                        else if (n.StartsWith("Éternue")) Bruit(a, "coup", 0.45);
                        else if (n.StartsWith("Siffle")) Bruit(a, "note", 0.1);
                        break;
                    case "Sport":
                        if (n.StartsWith("Tir") || n.StartsWith("Lancer") || n.StartsWith("Bowling") || n.StartsWith("Swing") || n.StartsWith("Service")) Bruit(a, "swish", 0.35);
                        else if (n.StartsWith("Burpee")) Bruit(a, "saut", 0.7);
                        break;
                    case "Gestes":
                        if (n.StartsWith("Yes") || n.StartsWith("Idée")) Bruit(a, "note", 0.1);
                        break;
                }
            }
        }

        static void Bruit(Anim a, string son, double quand)
        {
            a.Son = son;
            a.SonA = quand;
        }

        // ------------------------------------------------------------ attentes (entre deux actions)

        static void Attentes()
        {
            Repos.Add(Ajouter(null, "Debout", 3.2, 1, t =>
            {
                double[] p = L(S);
                double r = Math.Sin(2 * Math.PI * t) * R.D("respiration") / 100;
                p[I.Torse] += 1.5 * r; p[I.Ep1] += 2 * r; p[I.Ep2] -= 2 * r; p[I.Co1] += 2 * r; p[I.Tete] -= r;
                return p;
            }));
            Repos.Add(Ajouter(null, "Décontracté", 4.0, 1, t =>
            {
                double[] p = L("-2 0 4 8 -14 16 10 -6 -8 0");
                double r = Math.Sin(2 * Math.PI * t) * R.D("respiration") / 100;
                p[I.Torse] += 2 * r; p[I.Ha1] += 2 * r; p[I.Ge1] -= 3 * Math.Abs(r);
                return p;
            }));
            Repos.Add(Ajouter(null, "Poids sur une jambe", 4.4, 1, t =>
            {
                double[] p = L("3 2 -30 100 -12 14 2 0 -14 -12");
                p[I.Torse] += 1.5 * Math.Sin(2 * Math.PI * t) * R.D("respiration") / 100;
                return p;
            }));
        }

        // ------------------------------------------------------------ déplacements

        // A = écart des jambes, K = levée du genou, B = balancier des bras, C = pli du coude.
        static Anim Pas(string nom, double A, double K, double B, double C, double penche, double rebond, double v, double T,
            int bras = 0, double flex = 0, double assise = 0, bool robot = false)
        {
            Anim a = Ajouter("Déplacements", nom, T, 1, t =>
            {
                if (robot) t = Math.Floor(t * 8) / 8;
                double f = 2 * Math.PI * t, s = Math.Sin(f), c = Math.Cos(f);
                var p = new double[I.N];
                p[I.Ha1] = A * s + assise; p[I.Ha2] = -A * s + assise;
                p[I.Ge1] = -(flex + K * Math.Max(0, c)); p[I.Ge2] = -(flex + K * Math.Max(0, -c));
                p[I.Torse] = penche + 2 * Math.Sin(2 * f); p[I.Tete] = -penche * 0.4;
                p[I.Air] = rebond * Math.Abs(s);
                switch (bras)
                {
                    case 0: p[I.Ep1] = -B * s; p[I.Ep2] = B * s; p[I.Co1] = C + 8 * Math.Max(0, -s); p[I.Co2] = C + 8 * Math.Max(0, s); break;
                    case 1: p[I.Ep1] = 88 + 4 * s; p[I.Ep2] = 82 - 4 * s; p[I.Co1] = 5; p[I.Co2] = 8; break;                 // bras tendus devant
                    case 2: p[I.Ep1] = -75; p[I.Ep2] = -68; p[I.Co1] = -8; p[I.Co2] = -8; break;                             // bras en arrière
                    case 3: p[I.Ep1] = 165 + 15 * Math.Sin(3 * f); p[I.Ep2] = -165 + 15 * Math.Sin(3 * f + 2); p[I.Co1] = 15; p[I.Co2] = -15; break;
                    case 4: p[I.Ep1] = -35; p[I.Co1] = 105; p[I.Ep2] = -45; p[I.Co2] = 112; break;                           // mains sur les hanches
                    case 5: p[I.Ep1] = 50; p[I.Co1] = 110; p[I.Ep2] = 40; p[I.Co2] = 118; break;                             // mains repliées
                    case 6: p[I.Ep1] = 90 + 14 * Math.Sin(2 * f); p[I.Ep2] = -90 + 14 * Math.Sin(2 * f); p[I.Torse] += 4 * Math.Sin(2 * f); break;   // bras en balancier
                }
                return p;
            });
            a.Deplace = true;
            a.Vitesse = v;
            return a;
        }

        static void Deplacements()
        {
            Marche = Pas("Marche", 26, 40, 24, 15, 4, 0, 70, 0.9);
            Pas("Marche lente", 16, 26, 12, 10, 2, 0, 35, 1.4);
            Pas("Marche rapide", 32, 50, 40, 60, 8, 0, 120, 0.6);
            Pas("Petit trot", 32, 70, 30, 85, 10, 4, 150, 0.55);
            Course = Pas("Course", 45, 95, 50, 90, 16, 7, 240, 0.45);
            Pas("Sprint", 55, 110, 65, 95, 24, 10, 360, 0.36);
            Pas("Pas de loup", 30, 60, 10, 70, 22, 0, 40, 1.3, 0, 25, 20);
            Pas("Marche militaire", 40, 12, 55, 5, 0, 0, 80, 0.8);
            Pas("Genoux hauts", 42, 110, 40, 90, -4, 3, 60, 0.6);
            Pas("Zombie", 14, 15, 0, 0, 12, 0, 25, 1.6, 1);
            Pas("Course ninja", 50, 100, 0, 0, 40, 5, 320, 0.4, 2);
            Pas("Marche accroupie", 22, 30, 10, 60, 30, 0, 45, 0.9, 0, 70, 45);
            Pas("Moonwalk", 22, 8, 10, 10, -4, 0, -60, 1.0);
            Pas("Marche arrière", 20, 30, 15, 15, -3, 0, -45, 1.0);
            Pas("Sautille", 30, 80, 35, 60, 4, 14, 110, 0.6);
            Pas("Démarche cool", 22, 30, 32, 20, -6, 2, 55, 1.1);
            Pas("Sur la pointe des pieds", 14, 30, 0, 0, 0, 3, 45, 0.5, 5);
            Pas("Fuite paniquée", 45, 90, 0, 0, 12, 6, 260, 0.4, 3);
            Pas("Mains sur les hanches (marche)", 24, 35, 0, 0, -3, 0, 60, 1.0, 4);
            Pas("Robot", 24, 40, 30, 80, 0, 0, 50, 1.0, 0, 0, 0, true);
            Pas("Pas de géant", 50, 30, 45, 10, 6, 2, 110, 1.2);
            Pas("Petits pas pressés", 12, 25, 20, 80, 6, 1, 90, 0.3);
        }

        // ------------------------------------------------------------ danses : un mouvement de bras x un mouvement de jambes

        static void Danses()
        {
            var nomsBras = new[] { "Poings en l'air", "Disco", "Vague", "Mains en l'air", "Moulinets", "Déhanché", "Tape des mains", "Boxe",
                "Twist des bras", "Robot", "Guitare", "Lasso", "Égyptien", "Poulet", "Dab", "Rouleau" };
            var bras = new Action<double, double, double, double[]>[]
            {
                (f, s, c, p) => { p[I.Ep1] = 125 + 45 * s; p[I.Co1] = 35; p[I.Ep2] = 125 - 45 * s; p[I.Co2] = 35; },
                (f, s, c, p) => { p[I.Ep1] = 85 + 70 * s; p[I.Co1] = 8; p[I.Ep2] = -25 - 10 * s; p[I.Co2] = 100; p[I.Tete] -= 8 * s; },
                (f, s, c, p) => { p[I.Ep1] = 90 + 35 * s; p[I.Co1] = 30 * Math.Sin(f - 1); p[I.Ep2] = -90 - 35 * s; p[I.Co2] = -30 * Math.Sin(f + 2.1); },
                (f, s, c, p) => { p[I.Ep1] = 165 + 14 * s; p[I.Co1] = 12; p[I.Ep2] = -165 + 14 * s; p[I.Co2] = -12; },
                (f, s, c, p) => { p[I.Ep1] = f * 57.2958; p[I.Co1] = 15; p[I.Ep2] = f * 57.2958 + 180; p[I.Co2] = 15; },
                (f, s, c, p) => { p[I.Ep1] = -35; p[I.Co1] = 105; p[I.Ep2] = -45; p[I.Co2] = 112; p[I.Torse] += 6 * s; },
                (f, s, c, p) => { p[I.Ep1] = 155 + 18 * c; p[I.Co1] = 18; p[I.Ep2] = 205 - 18 * c; p[I.Co2] = -18; },
                (f, s, c, p) => { p[I.Ep1] = 75; p[I.Co1] = 95 - 90 * Math.Max(0, s); p[I.Ep2] = 65; p[I.Co2] = 100 - 95 * Math.Max(0, -s); },
                (f, s, c, p) => { p[I.Ep1] = 45 + 35 * s; p[I.Co1] = 85; p[I.Ep2] = 45 - 35 * s; p[I.Co2] = 85; },
                (f, s, c, p) => { double q = Math.Round(s); p[I.Ep1] = 90 * Math.Max(0, q); p[I.Co1] = 90; p[I.Ep2] = 90 * Math.Max(0, -q); p[I.Co2] = 90; },
                (f, s, c, p) => { p[I.Ep1] = 35; p[I.Co1] = 75 + 25 * Math.Sin(4 * f); p[I.Ep2] = 105; p[I.Co2] = 75; p[I.Torse] -= 8; p[I.Tete] += 10 * Math.Sin(2 * f); },
                (f, s, c, p) => { p[I.Ep1] = 165 + 18 * s; p[I.Co1] = 45 + 40 * c; p[I.Ep2] = -30; p[I.Co2] = 100; },
                (f, s, c, p) => { p[I.Ep1] = 92 + 6 * s; p[I.Co1] = 88; p[I.Ep2] = -92 + 6 * s; p[I.Co2] = 88; p[I.Tete] += 6 * s; },
                (f, s, c, p) => { p[I.Ep1] = 35 + 40 * Math.Abs(s); p[I.Co1] = 140; p[I.Ep2] = -35 - 40 * Math.Abs(s); p[I.Co2] = -140; },
                (f, s, c, p) => { double w = (1 + Math.Tanh(4 * s)) / 2; p[I.Ep1] = 118 + 14 * w; p[I.Co1] = 150 - 150 * w; p[I.Ep2] = 132 - 14 * w; p[I.Co2] = 150 * w; p[I.Tete] += 26; p[I.Torse] += 16; },
                (f, s, c, p) => { p[I.Ep1] = 70 + 15 * Math.Sin(2 * f); p[I.Co1] = 95 + 15 * Math.Cos(2 * f); p[I.Ep2] = 70 - 15 * Math.Sin(2 * f); p[I.Co2] = 95 - 15 * Math.Cos(2 * f); },
            };
            var nomsJambes = new[] { "rebond", "pas chassés", "coups de pied", "twist", "course sur place", "sauts", "squats", "talons" };
            var tempos = new[] { 0.7, 0.9, 0.8, 0.8, 0.6, 0.7, 1.1, 0.8 };
            var jambes = new Action<double, double, double, double[]>[]
            {
                (f, s, c, p) => { double d = Math.Abs(s); p[I.Ha1] = 8 + 14 * d; p[I.Ge1] = -28 * d; p[I.Ha2] = -8 + 14 * d; p[I.Ge2] = -28 * d; },
                (f, s, c, p) => { p[I.X] = 14 * s; p[I.Ha1] = 10 + 8 * s; p[I.Ha2] = -10 + 8 * s; p[I.Ge1] = p[I.Ge2] = -(6 + 14 * Math.Abs(c)); },
                (f, s, c, p) => { p[I.Ha1] = 8 + 62 * Math.Max(0, s); p[I.Ge1] = -8; p[I.Ha2] = -8 + 62 * Math.Max(0, -s); p[I.Ge2] = -8; p[I.Air] = 4 * Math.Abs(s); p[I.Torse] = -6 * Math.Abs(s); },
                (f, s, c, p) => { p[I.Ha1] = 24 + 12 * s; p[I.Ha2] = 4 + 12 * s; p[I.Ge1] = p[I.Ge2] = -32; p[I.Torse] = -8 * s; },
                (f, s, c, p) => { p[I.Ha1] = 38 * s; p[I.Ge1] = -(10 + 70 * Math.Max(0, c)); p[I.Ha2] = -38 * s; p[I.Ge2] = -(10 + 70 * Math.Max(0, -c)); p[I.Air] = 3 * Math.Abs(s); },
                (f, s, c, p) => { double u = Math.Max(0, s), d = Math.Max(0, -s); p[I.Air] = 26 * u * u; p[I.Ha1] = 8 + 10 * u + 17 * d; p[I.Ha2] = -8 - 10 * u + 17 * d; p[I.Ge1] = p[I.Ge2] = -34 * d; },
                (f, s, c, p) => { double d = (1 - c) / 2; p[I.Ha1] = 8 + 62 * d; p[I.Ge1] = -112 * d; p[I.Ha2] = -4 + 56 * d; p[I.Ge2] = -104 * d; p[I.Torse] = 22 * d; },
                (f, s, c, p) => { p[I.Ha1] = 6 + 28 * Math.Max(0, s); p[I.Ge1] = -10 * Math.Max(0, -s); p[I.Ha2] = -6 + 34 * Math.Max(0, -s); p[I.Ge2] = -10 * Math.Max(0, s); },
            };
            for (int i = 0; i < bras.Length; i++)
                for (int j = 0; j < jambes.Length; j++)
                {
                    var b = bras[i];
                    var g = jambes[j];
                    Anim a = Ajouter("Danses", nomsBras[i] + " + " + nomsJambes[j], tempos[j], 6, t =>
                    {
                        double f = 2 * Math.PI * t, s = Math.Sin(f), c = Math.Cos(f);
                        var p = new double[I.N];
                        g(f, s, c, p);
                        b(f, s, c, p);
                        return p;
                    });
                    a.Objet = (i + j) % 5 == 0 ? "note" : null;
                    if (i == 1 && j == 1) Danse = a;
                }
        }

        // ------------------------------------------------------------ gestes (haut du corps)

        static void Gestes()
        {
            const string F = "Gestes";
            Salut = Osc(F, "Salut", "0 -5 150 30 -8 12", "0 -5 150 80 -8 12", 0.5, 4);
            Autre(Salut);
            Autre(Osc(F, "Grand salut", "-4 -8 175 -20 -10 12", "-4 -8 150 40 -10 12", 0.6, 4));
            Autre(Osc(F, "Salut timide", "4 10 70 110 -5 10", "4 10 70 140 -5 10", 0.4, 4));
            Osc(F, "Coucou à deux mains", "0 -6 150 30 160 -30", "0 -6 150 75 160 -75", 0.5, 4);
            Autre(Osc(F, "Pointe devant", "4 0 90 0 -10 12", "6 0 93 4 -10 12", 0.8, 2));
            Autre(Osc(F, "Pointe en haut", "-6 -20 165 0 -10 12", "-6 -22 169 3 -10 12", 0.8, 2));
            Autre(Osc(F, "Pointe derrière", "-4 0 -95 0 10 12", "-4 0 -91 -4 10 12", 0.8, 2));
            Autre(Osc(F, "Poing levé", "0 -8 178 0 -10 15", "0 -8 168 22 -10 15", 0.4, 3));
            Osc(F, "Applaudit", "2 0 45 75 78 45", "2 0 60 62 62 58", 0.3, 6);
            Osc(F, "Se frotte les mains", "8 6 55 70 60 66", "8 6 60 66 55 70", 0.25, 6);
            Osc(F, "Bras croisés", "-2 0 35 115 45 105", "-2 2 36 116 44 104", 1.5, 2);
            Osc(F, "Mains sur les hanches", "-3 -3 -35 105 -45 112", "-3 -5 -36 107 -44 110", 1.5, 2);
            Autre(Osc(F, "Regarde au loin", "6 -6 110 125 -12 14", "8 -6 112 123 -12 14", 1.2, 2));
            Autre(Osc(F, "Facepalm", "8 22 80 135 -8 12", "8 26 80 137 -8 12", 1.2, 2));
            Osc(F, "Hausse les épaules", "0 -6 35 80 25 85", "0 4 15 30 5 35", 0.5, 3);
            Autre(Osc(F, "Viens ici", "2 0 85 20 -8 12", "2 0 85 120 -8 12", 0.5, 3));
            Autre(Osc(F, "Stop", "-3 0 88 8 -8 12", "-3 0 91 5 -8 12", 1.0, 2));
            Autre(Osc(F, "Salut militaire", "0 -2 95 135 -4 4", "0 -2 96 136 -4 4", 1.5, 1));
            Autre(Osc(F, "Envoie un bisou", "4 5 70 140 -8 12", "0 -5 95 10 -8 12", 0.9, 2, "coeur"));
            Autre(Osc(F, "Dab", "20 30 130 -5 120 150", "22 32 132 -5 122 150", 1.0, 2));
            Autre(Osc(F, "Coup de chapeau", "6 10 120 120 -8 12", "0 0 130 60 -8 12", 0.7, 2));
            Autre(Osc(F, "Fait non du doigt", "0 0 80 95 -8 12", "0 0 80 75 -8 12", 0.3, 5));
            Osc(F, "Montre ses muscles", "-4 -5 95 100 -95 -100", "-4 -5 100 115 -100 -115", 0.6, 3);
            Autre(Osc(F, "Se gratte la tête", "4 8 150 105 -8 12", "4 8 150 125 -8 12", 0.25, 6));
            Autre(Osc(F, "Réfléchit", "6 10 55 125 20 95", "6 14 55 127 20 95", 1.5, 2, "question"));
            Autre(Osc(F, "Bâille", "-8 -15 100 130 -8 12", "-10 -20 100 132 -8 12", 1.4, 1));
            Osc(F, "S'étire", "-10 -15 170 15 -170 -15", "-14 -20 178 5 -178 -5", 1.2, 2);
            Autre(Osc(F, "Regarde sa montre", "6 20 60 85 -8 12", "6 22 60 87 -8 12", 1.3, 1));
            Autre(Osc(F, "Téléphone", "0 -5 110 140 -8 12", "0 -3 110 142 -8 12", 1.5, 2, "telephone"));
            Autre(Osc(F, "Yes !", "0 -5 40 130 -8 12", "-4 -8 60 150 -8 12", 0.35, 3));
            Osc(F, "Hoche la tête", "0 14 10 12 -10 12", "0 -8 10 12 -10 12", 0.4, 4);
            Osc(F, "Mains dans le dos", "-3 -3 -30 -60 -35 -55", "-3 -6 -31 -60 -36 -55", 1.6, 2);
            Autre(Osc(F, "Boit", "0 0 60 120 -8 12", "-10 -25 75 135 -8 12", 1.2, 2));
            Autre(Osc(F, "Lit", "8 22 45 95 50 90", "8 24 46 96 51 89", 1.6, 3, "livre"));
            Autre(Osc(F, "Idée !", "0 -10 165 0 -8 12", "0 -12 168 2 -8 12", 0.7, 2, "exclam"));
        }

        // ------------------------------------------------------------ combat

        static Anim Coup(string nom, string arme, string frappe, string objet = null)
        {
            return Cles("Combat", nom, "0:" + G + ";130:" + arme + ";220:" + frappe + ";300:" + frappe + ";520:" + G, objet);
        }

        static void Combat()
        {
            const string F = "Combat";
            const string armePoing = "4 0 -15 120 55 110 10 -18 -16 -12";
            const string armePied = "-6 0 60 95 45 105 75 -115 -8 -12";
            Poing = Coup("Coup de poing", armePoing, "16 0 90 0 50 115 24 -22 -22 -6 0 0 8");
            Autre(Poing);
            PoingHaut = Coup("Coup de poing haut", armePoing, "8 -8 122 0 50 115 22 -18 -20 -6 0 0 6");
            Autre(PoingHaut);
            Autre(Coup("Coup de poing bas", armePoing, "26 6 62 0 50 115 30 -32 -22 -10 0 0 8"));
            Autre(Coup("Crochet", "0 0 -40 60 55 110 10 -18 -16 -12", "14 0 85 65 50 115 22 -20 -20 -6 0 0 6"));
            Autre(Coup("Uppercut", "22 5 10 95 55 110 30 -45 -10 -30", "-6 -12 140 45 50 115 14 -8 -14 -4 6 0 4"));
            Autre(Coup("Coup de coude", armePoing, "12 0 80 150 50 115 20 -20 -18 -6 0 0 8"));
            Coup("Double poing", "-4 0 -20 115 -25 118 10 -18 -16 -12", "20 0 92 0 88 4 26 -24 -24 -6 0 0 10");
            PiedMoyen = Coup("Coup de pied", armePied, "-16 0 55 100 30 110 92 -4 -8 -10");
            Autre(PiedMoyen, " (autre jambe)");
            Autre(Coup("Coup de pied haut", armePied, "-30 -5 40 100 20 110 132 -4 -10 -8"), " (autre jambe)");
            PiedBas = Coup("Coup de pied bas", armePied, "-4 0 60 95 45 105 52 -2 -8 -12");
            Autre(PiedBas, " (autre jambe)");
            Autre(Coup("Coup de genou", armePied, "10 0 80 95 70 100 95 -125 -6 -6 2"), " (autre jambe)");
            Autre(Coup("Coup de pied arrière", "25 0 60 95 50 100 40 -110 -6 -12", "48 -10 70 80 60 90 -78 -2 -4 -14"), " (autre jambe)");
            Coup("Balayage", Cr, "20 0 20 20 -60 -10 85 -6 70 -125");
            Cles(F, "Coup de pied sauté", "0:" + G + ";130:" + Cr + ";280:-25 -5 40 100 20 110 120 -4 20 -90 38;420:-10 0 60 95 45 105 60 -60 10 -50 20;560:" + Cr + ";720:" + G);
            Osc(F, "Garde haute", "6 0 110 80 100 85 14 -15 -14 -10", "8 2 112 78 102 83 14 -18 -14 -12", 0.6, 3);
            Osc(F, "Garde basse", "20 10 50 60 40 70 25 -40 -10 -35", "22 12 52 58 42 68 27 -44 -8 -38", 0.6, 3);
            Osc(F, "Sautille en garde", "8 0 70 100 55 110 14 -15 -14 -10", "8 0 72 98 57 108 14 -22 -14 -18 6", 0.3, 8);
            Cles(F, "Esquive arrière", "0:" + G + ";140:-28 -10 20 60 10 70 20 -8 -14 -30 0 0 -8;320:-28 -10 20 60 10 70 20 -8 -14 -30 0 0 -8;520:" + G);
            Cles(F, "Esquive basse", "0:" + G + ";140:30 10 80 110 70 115 70 -118 55 -108;340:30 10 80 110 70 115 70 -118 55 -108;540:" + G);

            // enchaînements : les coups ci-dessus mis bout à bout
            double[] g = L(G), jab = L("16 0 90 0 50 115 24 -22 -22 -6 0 0 8"), croise = Echange(jab), haut = L("8 -8 122 0 50 115 22 -18 -20 -6 0 0 6"),
                pied = L("-16 0 55 100 30 110 92 -4 -8 -10"), piedHaut = L("-30 -5 40 100 20 110 132 -4 -10 -8"), upper = L("-6 -12 140 45 50 115 14 -8 -14 -4 6 0 4");
            ClesP(F, "Enchaînement de 2 coups", new[] { 0, 0.15, 0.3, 0.45, 0.6, 0.85 }, new[] { g, jab, g, croise, croise, g });
            ClesP(F, "Enchaînement de 3 coups", new[] { 0, 0.15, 0.3, 0.45, 0.6, 0.8, 0.95, 1.2 }, new[] { g, jab, g, croise, g, pied, pied, g });
            ClesP(F, "Enchaînement de 5 coups", new[] { 0, 0.14, 0.28, 0.42, 0.56, 0.72, 0.88, 1.04, 1.2, 1.4, 1.55, 1.85 },
                new[] { g, jab, g, croise, g, haut, g, upper, g, piedHaut, piedHaut, g });
            ClesP(F, "Rafale de coups de poing", new[] { 0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 1.0 }, new[] { g, jab, croise, jab, croise, jab, croise, jab, croise, g });

            // épée
            const string gardeEpee = "6 0 40 80 -20 60 14 -15 -14 -10";
            Fixe(F, "Garde à l'épée", gardeEpee, 2, "epee");
            Cles(F, "Épée : taille haute", "0:" + gardeEpee + ";160:-8 -5 160 20 -30 50 10 -12 -16 -8;260:22 5 35 5 -40 40 26 -24 -22 -6 0 0 8;360:22 5 35 5 -40 40 26 -24 -22 -6 0 0 8;600:" + gardeEpee, "epee");
            Cles(F, "Épée : taille montante", "0:" + gardeEpee + ";160:18 5 -30 10 -30 50 20 -25 -16 -10;260:-6 -8 150 10 -40 40 16 -10 -18 -4 0 0 6;360:-6 -8 150 10 -40 40 16 -10 -18 -4 0 0 6;600:" + gardeEpee, "epee");
            Cles(F, "Épée : estoc", "0:" + gardeEpee + ";150:4 0 10 100 -30 50 10 -15 -16 -10;240:14 0 92 0 -50 30 34 -30 -28 -4 0 0 14;340:14 0 92 0 -50 30 34 -30 -28 -4 0 0 14;580:" + gardeEpee, "epee");
            Ajouter(F, "Épée : moulinet", 0.6, 4, t => { double[] p = L(gardeEpee); p[I.Ep1] = 40 + 360 * t; p[I.Co1] = 10; return p; }).Objet = "epee";
            Osc(F, "Épée : parade", "0 0 70 110 -20 60 14 -15 -14 -10", "-4 0 76 112 -22 60 12 -18 -16 -12", 0.5, 3, "epee");
            Cles(F, "Épée : double taille", "0:" + gardeEpee + ";140:-8 -5 160 20 -30 50 10 -12 -16 -8;230:22 5 35 5 -40 40 26 -24 -22 -6 0 0 8;360:18 5 -30 10 -30 50 20 -25 -16 -10;450:-6 -8 150 10 -40 40 16 -10 -18 -4 0 0 6;560:-6 -8 150 10 -40 40 16 -10 -18 -4;800:" + gardeEpee, "epee");

            // bâton et pouvoirs
            Ajouter(F, "Bâton : tourbillon", 0.7, 5, t => L("-4 -8 150 30 170 -10 16 -12 -16 -8")).Objet = "baton";
            Cles(F, "Bâton : frappe au sol", "0:" + G + ";200:-10 -10 165 10 170 5 14 -10 -16 -6;330:40 15 40 5 45 0 60 -95 30 -70;480:40 15 40 5 45 0 60 -95 30 -70;760:" + G, "baton2");
            Cles(F, "Boule d'énergie", "0:" + G + ";450:-12 0 -35 70 -25 80 20 -30 -18 -25;900:-14 0 -38 72 -28 82 22 -34 -18 -28;1020:18 0 92 2 86 6 28 -26 -26 -6 0 0 10;1500:18 0 92 2 86 6 28 -26 -26 -6 0 0 10;1800:" + G, "boule");
            Cles(F, "Onde de choc", "0:" + S + ";160:" + Cr + ";400:-10 -10 170 10 160 20 30 -60 10 -50 60;600:40 20 30 0 80 60 80 -120 60 -110;1100:40 20 30 0 80 60 80 -120 60 -110;1400:" + S, "onde");
            Osc(F, "Concentre son énergie", "0 -10 -20 100 -30 105 16 -22 -16 -22", "-3 -14 -24 104 -34 108 18 -26 -18 -26", 0.2, 14, "aura");
            Osc(F, "Provocation", "4 0 85 30 55 110 14 -15 -14 -10", "4 0 85 120 55 110 14 -15 -14 -10", 0.45, 4);
            Cles(F, "Salut martial", "0:" + S + ";300:45 15 60 80 55 85 6 0 -6 0;900:45 15 60 80 55 85 6 0 -6 0;1200:" + S);
        }

        // ------------------------------------------------------------ acrobaties

        static void Acrobaties()
        {
            const string F = "Acrobaties";
            const string haut = "-4 -10 172 5 -172 -5 5 0 -5 0";
            Cles(F, "Saut", "0:" + S + ";160:" + Cr + ";300:" + haut + " 30;430:" + haut + " 48;560:8 0 60 30 50 30 30 -50 20 -45 22;700:" + Cr + ";900:" + S);
            Cles(F, "Grand saut", "0:" + S + ";200:" + Cr + ";380:" + haut + " 60;560:" + haut + " 95;740:8 0 60 30 50 30 30 -50 20 -45 50;900:" + Cr + ";1150:" + S);
            Cles(F, "Saut groupé", "0:" + S + ";160:" + Cr + ";300:" + haut + " 30;440:" + Boule + " 58;580:8 0 60 30 50 30 30 -50 20 -45 24;700:" + Cr + ";900:" + S);
            Cles(F, "Saut en étoile", "0:" + S + ";160:" + Cr + ";300:" + haut + " 28;440:0 -5 150 0 -150 0 45 0 -45 0 52;580:8 0 60 30 50 30 30 -50 20 -45 22;700:" + Cr + ";900:" + S);
            Cles(F, "Saut carpé", "0:" + S + ";160:" + Cr + ";300:" + haut + " 30;440:60 10 80 0 78 0 95 0 90 0 55;580:8 0 60 30 50 30 30 -50 20 -45 22;700:" + Cr + ";900:" + S);
            Cles(F, "Salto avant", "0:" + S + ";140:" + Cr + ";260:-5 -10 160 10 -170 -10 8 -5 -4 -5 30 20;400:" + Boule + " 78 150;540:" + Boule + " 72 285;660:5 0 80 20 70 25 40 -40 25 -35 22 350;760:" + Cr + " 0 360;950:" + S + " 0 360", null, 60);
            Cles(F, "Salto arrière", "0:" + S + ";140:" + Cr + ";260:-15 -20 170 0 -175 0 8 -5 -4 -5 30 -25;400:" + Boule + " 78 -150;540:" + Boule + " 72 -285;660:5 0 80 20 70 25 40 -40 25 -35 22 -350;760:" + Cr + " 0 -360;950:" + S + " 0 -360", null, -50);
            Cles(F, "Double salto avant", "0:" + S + ";160:" + Cr + ";300:-5 -10 160 10 -170 -10 8 -5 -4 -5 45 20;460:" + Boule + " 115 200;620:" + Boule + " 125 400;780:" + Boule + " 95 600;900:5 0 80 20 70 25 40 -40 25 -35 30 705;1000:" + Cr + " 0 720;1200:" + S + " 0 720", null, 70);
            Cles(F, "Double salto arrière", "0:" + S + ";160:" + Cr + ";300:-15 -20 170 0 -175 0 8 -5 -4 -5 45 -25;460:" + Boule + " 115 -200;620:" + Boule + " 125 -400;780:" + Boule + " 95 -600;900:5 0 80 20 70 25 40 -40 25 -35 30 -705;1000:" + Cr + " 0 -720;1200:" + S + " 0 -720", null, -60);
            const string etoile = "0 0 170 0 190 0 35 0 -35 0";
            Cles(F, "Roue", "0:-4 -8 170 0 190 0 20 0 -20 0;250:" + etoile + " 0 90;500:" + etoile + " 0 180;750:" + etoile + " 0 270;1000:-4 -8 170 0 190 0 20 0 -20 0 0 360;1200:" + S + " 0 360", null, 110);
            Cles(F, "Roue arrière", "0:-4 -8 170 0 190 0 20 0 -20 0;250:" + etoile + " 0 -90;500:" + etoile + " 0 -180;750:" + etoile + " 0 -270;1000:-4 -8 170 0 190 0 20 0 -20 0 0 -360;1200:" + S + " 0 -360", null, -110);
            const string poirier = "0 0 180 0 178 0 4 0 -4 0 0 180";
            Cles(F, "Poirier", "0:" + S + ";300:80 10 150 0 145 0 30 -40 -30 0 0 40;600:" + poirier + ";1200:0 5 180 0 178 0 14 -10 -14 -10 0 176;1800:" + poirier + ";2100:80 10 150 0 145 0 30 -40 -30 0 0 40;2400:" + S);
            Anim mains = Ajouter(F, "Marche sur les mains", 0.8, 6, t =>
            {
                double s = Math.Sin(2 * Math.PI * t);
                double[] p = L(poirier);
                p[I.Ep1] = 180 + 18 * s; p[I.Ep2] = 180 - 18 * s; p[I.Ha1] = 10 + 14 * s; p[I.Ha2] = -10 - 14 * s; p[I.Ge1] = -12; p[I.Ge2] = -12;
                return p;
            });
            mains.Vitesse = 40;
            Cles(F, "Équilibre sur une main", "0:" + S + ";300:80 10 150 0 145 0 30 -40 -30 0 0 40;600:" + poirier + ";900:0 0 180 0 100 20 20 -30 -25 0 0 180;1900:0 0 180 0 104 22 24 -34 -28 0 0 178;2200:" + poirier + ";2500:80 10 150 0 145 0 30 -40 -30 0 0 40;2800:" + S);
            Cles(F, "Roulade avant", "0:" + S + ";150:" + Cr + ";300:60 40 60 80 55 85 100 -130 95 -125 0 90;450:" + Boule + " 0 180;600:" + Boule + " 0 270;750:" + Cr + " 0 360;950:" + S + " 0 360", null, 110);
            Cles(F, "Roulade arrière", "0:" + S + ";150:" + Cr + ";300:" + Boule + " 0 -90;450:" + Boule + " 0 -180;600:" + Boule + " 0 -270;750:" + Cr + " 0 -360;950:" + S + " 0 -360", null, -110);
            Cles(F, "Saut de mains", "0:" + S + ";150:10 0 170 0 172 0 30 -30 -20 -10;300:0 0 180 0 178 0 30 0 -30 0 0 120;450:" + poirier + ";600:-20 -10 180 0 178 0 20 -10 -10 -10 30 270;750:" + Cr + " 0 360;950:" + S + " 0 360", null, 100);
            Cles(F, "Flip-flap", "0:" + S + ";150:-10 -10 170 0 -170 0 30 -50 20 -45;300:-20 -20 180 0 178 0 20 -10 -10 -10 30 -90;450:0 0 180 0 178 0 4 0 -4 0 0 -180;600:30 10 180 0 178 0 60 0 55 0 25 -270;750:" + Cr + " 0 -360;900:" + S + " 0 -360", null, -90);
            Cles(F, "Kip-up", "0:" + S + ";300:-10 0 -30 -8 -40 -8 120 -60 112 -52;600:" + Dos + ";900:" + Dos + ";1100:0 0 20 10 25 5 100 0 96 0 0 -100;1250:-10 0 -40 20 -50 20 60 -40 50 -35 40 -30;1380:" + Cr + " 10;1480:" + Cr + ";1700:" + S);
            Cles(F, "Grand écart", "0:" + S + ";500:0 0 170 0 -170 0 90 0 -90 0;1700:0 -4 175 0 -175 0 90 0 -90 0;2200:" + S);
            Osc(F, "Toupie sur la tête", "0 0 120 40 -120 -40 40 -20 -40 -20 0 180", "0 0 120 40 -120 -40 -40 -20 40 -20 0 180", 0.4, 8);
            Fixe(F, "Freeze de breakdance", "0 0 160 60 30 100 100 -60 60 -120 0 150", 2);
            Cles(F, "Saut en longueur", "0:" + S + ";160:" + Cr + ";300:10 -5 150 10 -120 -10 60 -40 -30 -20 35;480:30 0 100 10 90 10 95 -20 85 -15 42;640:30 5 60 20 50 20 80 -70 70 -65 12;760:" + Cr + ";960:" + S, null, 220);
            Cles(F, "Plongeon roulé", "0:" + S + ";150:" + Cr + ";300:0 -10 170 0 175 0 -10 0 -16 0 40 70;450:60 40 60 80 55 85 100 -130 95 -125 10 180;600:" + Boule + " 0 270;750:" + Cr + " 0 360;950:" + S + " 0 360", null, 170);
        }

        // ------------------------------------------------------------ sport

        static void Sport()
        {
            const string F = "Sport";
            Osc(F, "Pompes", Planche, "0 -10 25 135 27 135 0 0 2 0 0 80", 1.0, 6);
            Osc(F, "Abdos", "0 0 150 100 150 100 75 -120 70 -115 0 -75", "15 0 100 140 100 140 120 -60 115 -55", 1.4, 5);
            Osc(F, "Squats", "0 0 80 10 80 10 6 0 -6 0", "30 -15 85 5 85 5 75 -120 65 -112", 1.2, 5);
            Autre(Osc(F, "Fentes", S, "5 0 -35 105 -45 112 70 -85 -40 -75", 1.3, 4), " (autre jambe)");
            Osc(F, "Jumping jacks", "0 0 10 5 -10 5 4 0 -4 0", "0 0 170 0 -170 0 25 0 -25 0 10", 0.6, 8);
            Osc(F, "Touche ses pieds", "-5 -10 175 0 -175 0 4 0 -4 0", "95 15 20 0 15 0 4 0 -4 0", 1.8, 3);
            Fixe(F, "Planche", Planche, 3);
            Fixe(F, "Yoga : l'arbre", "0 0 175 8 -175 -8 0 0 55 -135", 3);
            Fixe(F, "Yoga : le guerrier", "0 0 90 0 -90 0 55 -75 -45 0", 3);
            Fixe(F, "Yoga : chien tête en bas", "0 15 180 0 178 0 90 0 86 0 0 120", 3);
            Fixe(F, "Yoga : le cobra", "-50 -10 75 0 78 0 -15 0 -12 0 0 75", 3);
            Fixe(F, "Yoga : la chandelle", "0 30 -150 20 -155 20 0 0 4 0 0 -170", 3);
            Osc(F, "Haltères", "0 0 10 10 8 10 8 0 -8 0", "0 0 20 140 18 140 8 0 -8 0", 1.0, 6, "haltere");
            Osc(F, "Développé au-dessus de la tête", "0 0 60 130 62 130 10 -10 -10 -10", "0 -8 178 0 180 0 8 0 -8 0", 1.1, 5, "haltere");
            Osc(F, "Corde à sauter", "0 0 20 60 -20 -60 6 -5 -6 -5", "0 0 22 62 -22 -62 8 -22 -8 -22 12", 0.4, 12, "corde");
            Cles(F, "Burpee", "0:" + S + ";200:" + Cr + ";400:" + Planche + ";600:0 -10 25 135 27 135 0 0 2 0 0 80;800:" + Planche + ";1000:" + Cr + ";1150:-4 -10 172 5 -172 -5 5 0 -5 0 30;1320:" + Cr + ";1500:" + S);
            Cles(F, "Tir au but", "0:" + S + ";250:-8 0 60 20 -60 10 -50 -80 8 -10;380:-18 0 -40 20 80 10 82 -4 -6 -12 0 0 6;520:-18 0 -40 20 80 10 82 -4 -6 -12 0 0 6;800:" + S, "ballonpied");
            Osc(F, "Dribble", "14 12 30 40 -20 60 16 -22 -12 -20", "16 14 20 10 -20 60 18 -26 -12 -24", 0.4, 10, "ballonmain");
            Cles(F, "Tir au panier", "0:14 12 30 40 -20 60 16 -22 -12 -20;250:20 5 60 110 55 115 50 -90 40 -85;450:-6 -15 165 10 150 30 6 0 -6 0 35;600:-6 -15 170 -30 150 30 6 0 -6 0 30;800:" + Cr + ";1000:" + S, "ballontir");
            Cles(F, "Swing de golf", "0:20 15 35 5 40 0 14 -15 -14 -15;400:15 5 -120 -30 -110 -40 16 -18 -12 -14;560:22 15 35 5 40 0 16 -16 -14 -14;720:-8 -10 150 30 145 35 10 -8 -18 -20;1100:-8 -12 152 32 147 37 10 -8 -18 -20;1400:" + S, "club");
            Cles(F, "Service de tennis", "0:" + S + ";300:-6 -15 -60 80 165 5 10 -12 -10 -12;500:-10 -20 150 60 120 20 12 -20 -10 -20 8;600:25 5 60 0 -30 40 26 -26 -20 -8 0 0 8;800:25 5 30 5 -30 40 26 -26 -20 -8;1100:" + S, "raquette");
            Cles(F, "Lancer", "0:" + S + ";250:-12 -5 -120 60 70 20 -20 -10 20 -15;400:20 5 100 5 -40 30 28 -26 -24 -6 0 0 10;550:25 8 50 5 -40 30 28 -26 -24 -6 0 0 10;850:" + S, "ballontir");
            Cles(F, "Bowling", "0:" + S + ";300:15 5 -70 5 40 30 30 -30 -30 -10;500:35 10 50 0 -50 20 60 -85 -50 -30 0 0 10;750:35 10 110 0 -50 20 60 -85 -50 -30 0 0 10;1100:" + S, "ballonroule");
            Ajouter(F, "Nage", 1.0, 5, t =>
            {
                double f = 2 * Math.PI * t;
                double[] p = L("0 -15 0 10 0 10 0 0 0 0 26 80");
                p[I.Ep1] = 360 * t + 80; p[I.Ep2] = 360 * t + 260; p[I.Ha1] = 12 * Math.Sin(3 * f); p[I.Ha2] = -12 * Math.Sin(3 * f);
                p[I.Air] = 26 + 3 * Math.Sin(f);
                return p;
            });
            Ajouter(F, "Pédale", 0.7, 6, t =>
            {
                double f = 2 * Math.PI * t;
                double[] p = L("-20 5 -30 -8 -40 -8 0 0 0 0");
                p[I.Ha1] = 105 + 28 * Math.Sin(f); p[I.Ge1] = -70 - 40 * Math.Cos(f); p[I.Ha2] = 105 - 28 * Math.Sin(f); p[I.Ge2] = -70 + 40 * Math.Cos(f);
                return p;
            });
            Ajouter(F, "Grimpe", 0.9, 5, t =>
            {
                double s = Math.Sin(2 * Math.PI * t);
                double[] p = L("4 -12 0 0 0 0 0 0 0 0");
                p[I.Ep1] = 150 + 25 * s; p[I.Co1] = 30 - 25 * s; p[I.Ep2] = 150 - 25 * s; p[I.Co2] = 30 + 25 * s;
                p[I.Ha1] = 45 - 40 * s; p[I.Ge1] = -70 + 55 * s; p[I.Ha2] = 45 + 40 * s; p[I.Ge2] = -70 - 55 * s;
                p[I.Air] = 14 + 3 * Math.Abs(s);
                return p;
            });
            Osc(F, "Skateboard", "18 0 40 10 -60 -10 40 -70 20 -60", "22 4 55 15 -45 -5 46 -80 24 -68", 0.9, 4, "planche");
            Osc(F, "Surf", "14 -5 80 5 -85 -5 50 -80 -10 -45", "20 0 70 10 -95 0 56 -90 -6 -52", 1.1, 4, "planche");
        }

        // ------------------------------------------------------------ vie quotidienne

        static void Quotidien()
        {
            const string F = "Quotidien";
            Osc(F, "S'assoit", "-10 0 -30 -8 -40 -8 120 -60 112 -52", "-12 4 -30 -8 -40 -8 120 -60 112 -52", 2, 3);
            Osc(F, "Assis jambes tendues", "-15 0 -35 -5 -45 -5 90 0 84 0", "-15 5 -35 -5 -45 -5 90 0 84 0", 2, 3);
            Osc(F, "Assis, balance les pieds", "-10 0 -30 -8 -40 -8 100 -20 96 -70", "-10 2 -30 -8 -40 -8 96 -70 100 -20", 0.8, 6);
            Osc(F, "Méditation", "0 0 60 60 55 65 100 -160 95 -155 12", "0 2 60 60 55 65 100 -160 95 -155 20", 2.4, 4, "aura");
            Fixe(F, "À genoux", "0 0 5 10 -5 10 5 -95 -5 -90", 2);
            Dort = Osc(F, "Dort", Dos, "3 12 25 5 28 -5 15 0 12 0 0 -75", 3, 4, "zzz");
            Osc(F, "Sieste sur le ventre", Ventre, "0 3 165 20 170 10 -15 0 -12 0 0 73", 3, 4, "zzz");
            Osc(F, "Allongé, bras sous la tête", "0 -10 -150 120 -145 125 15 0 60 -100 0 -75", "2 -8 -150 120 -145 125 15 0 64 -104 0 -75", 2.5, 3);
            Osc(F, "Tape au clavier", "5 15 55 45 60 40 90 0 84 0", "5 15 58 42 57 43 90 0 84 0", 0.2, 16, "laptop");
            Ajouter(F, "Dessine", 2.0, 3, t =>
            {
                double f = 2 * Math.PI * t;
                double[] p = L("4 -5 0 0 -10 12 8 0 -8 0");
                p[I.Ep1] = 95 + 28 * Math.Sin(f); p[I.Co1] = 25 + 25 * Math.Cos(2 * f);
                return p;
            }).Objet = "crayon";
            Cles(F, "Éternue", "0:" + S + ";500:-15 -25 60 100 -8 12 6 0 -6 0;650:35 40 70 120 -8 20 14 -18 -4 -14;900:30 30 70 120 -8 20 12 -14 -4 -10;1300:" + S, "exclam");
            Osc(F, "Frissonne", "10 10 40 115 50 105 12 -15 -2 -15", "12 12 44 112 46 108 14 -18 0 -18", 0.12, 16);
            Osc(F, "Rit", "-12 -20 -30 100 -40 108 6 0 -6 0", "8 5 -30 100 -40 108 8 -10 -4 -10", 0.25, 8);
            Osc(F, "Soupire", S, "10 20 5 5 -5 5 6 0 -6 0", 1.6, 2);
            Osc(F, "Tape du pied", "0 0 35 115 45 105 6 0 -6 0", "0 0 35 115 45 105 24 -4 -6 0", 0.35, 8);
            Osc(F, "Siffle", "-4 -10 -30 -40 -35 -45 6 0 -6 0", "-4 -4 -30 -40 -35 -45 6 0 -6 0", 0.6, 5, "note");
            Cles(F, "S'incline", "0:" + S + ";350:70 20 -10 5 -15 5 6 0 -6 0;900:70 20 -10 5 -15 5 6 0 -6 0;1300:" + S);
            Cles(F, "Ramasse quelque chose", "0:" + S + ";350:60 20 40 0 10 10 60 -100 40 -90;700:62 22 44 0 10 10 62 -104 40 -92;1100:" + S);
            Osc(F, "Reprend son souffle", "45 15 28 0 22 0 14 -12 -4 -12", "50 18 28 0 22 0 14 -14 -4 -14", 0.5, 6);
            Osc(F, "Se balance", "-6 0 10 12 -10 12 10 0 -2 0", "6 0 10 12 -10 12 2 0 -10 0", 1.2, 4);
            Osc(F, "Sur la pointe des pieds", S, "-2 -8 4 8 -4 8 3 0 -3 0 6", 0.9, 4);
            Osc(F, "Regarde ses pieds", "14 30 10 12 -10 12 6 0 -6 0", "16 34 10 12 -10 12 12 -4 -6 0", 1.2, 3);
            Osc(F, "Regarde le ciel", "-10 -30 -30 -50 -35 -45 6 0 -6 0", "-12 -34 -30 -50 -35 -45 6 0 -6 0", 1.6, 3);
            Cles(F, "Trébuche", "0:" + S + ";150:25 10 80 20 60 30 40 -60 -30 -10 0 0 6;300:45 15 110 10 100 20 60 -20 -50 -30 0 20 12;480:20 5 40 40 -60 20 30 -50 -10 -30 0 0 16;800:" + S);
            Cles(F, "Glisse et tombe", "0:" + S + ";150:-20 -10 120 20 -100 -20 70 -10 10 -30 6 -20;350:" + Dos + ";1200:" + Dos + ";1500:-10 0 -30 -8 -40 -8 120 -60 112 -52;1800:" + Cr + ";2100:" + S, "exclam");
            Osc(F, "Fait la planche contre un mur", "-14 -6 -30 -30 -35 -25 24 0 14 0", "-16 -4 -30 -30 -35 -25 24 0 14 0", 2, 3);
        }

        // ------------------------------------------------------------ émotions

        static void Emotions()
        {
            const string F = "Émotions";
            Osc(F, "Joie", "0 -10 170 10 -170 -10 6 -5 -6 -5", "0 -15 175 0 -175 0 15 -30 -15 -30 28", 0.45, 5, "note");
            Osc(F, "Victoire", "-6 -15 160 -10 -160 10 8 0 -8 0", "-8 -18 165 -5 -165 5 8 0 -8 0 4", 0.6, 3);
            Fixe(F, "Fier", "-10 -12 -35 105 -45 112 8 0 -8 0", 3);
            Osc(F, "Colère", "8 5 -15 30 -25 30 6 0 -6 0", "10 8 -10 40 -20 40 40 -70 -6 0", 0.3, 6, "exclam");
            Osc(F, "Rage", "-5 -15 150 60 -150 -60 8 -5 -8 -5", "-5 -15 160 30 -160 -30 8 -14 -8 -14 6", 0.2, 8, "exclam");
            Osc(F, "Peur", "25 20 80 130 85 125 30 -50 20 -45", "27 22 84 127 81 128 32 -54 22 -48", 0.1, 20);
            Cles(F, "Surprise", "0:" + S + ";120:-15 -15 120 40 -120 -40 20 -10 -20 -10 14 0 -10;400:-12 -12 110 50 -110 -50 16 -8 -16 -8 0 0 -10;800:" + S, "exclam");
            Osc(F, "Tristesse", "18 30 5 5 -5 5 6 -5 -6 -5", "20 34 5 5 -5 5 6 -6 -6 -6", 1.5, 2);
            Osc(F, "Pleure", "25 35 95 140 100 135 6 -5 -6 -5", "28 38 95 142 100 137 6 -8 -6 -8", 0.5, 6);
            Osc(F, "Désespoir", "25 30 10 5 0 5 5 -95 -5 -90", "28 34 10 5 0 5 5 -95 -5 -90", 1.4, 3);
            Osc(F, "Amour", "0 5 50 120 55 115 6 0 -6 0", "-3 0 50 122 55 117 6 0 -6 0 3", 0.8, 4, "coeur");
            Osc(F, "Confusion", "4 8 150 105 -30 100 6 0 -6 0", "4 -6 150 125 -30 100 6 0 -6 0", 0.9, 3, "question");
            Osc(F, "Timide", "5 15 -30 -50 -35 -45 6 0 20 -40", "5 18 -30 -50 -35 -45 6 0 8 -30", 0.6, 4);
            Fixe(F, "Boude", "-5 -15 35 115 45 105 6 0 -6 0", 3);
            Osc(F, "Impatient", "0 0 -35 105 -45 112 6 0 -6 0", "0 4 -35 105 -45 112 22 -4 -6 0", 0.3, 8);
            Osc(F, "Danse de la victoire", "-6 -10 150 60 -30 100 20 -20 -10 -10", "6 -10 -30 100 150 -60 -10 -10 20 -20 4", 0.5, 6, "note");
            Osc(F, "Ennui", "10 22 60 120 -10 12 10 -4 -8 0", "12 26 60 122 -10 12 10 -4 -8 0", 2.0, 2);
        }
    }
}
