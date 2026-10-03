// Mascotte Claude : une petite compagne de bureau, dans l'esprit des pets de Codex.
// Fenêtre transparente toujours visible, posée en bas à droite de l'écran.
// Compilation : construire.ps1 (csc du .NET Framework, aucune dépendance à installer).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MascotteClaude
{
    static class Programme
    {
        [STAThread]
        static int Main(string[] args)
        {
            // MascotteClaude.exe --etat running|waiting|review|failed|idle|waving|jumping|walking [message]
            // transmet un état à la mascotte déjà lancée (pratique depuis un hook Claude Code).
            if (args.Length >= 2 && (args[0] == "--etat" || args[0] == "--state"))
            {
                Directory.CreateDirectory(Mascotte.Dossier);
                string message = string.Join(" ", args, 2, args.Length - 2);
                File.WriteAllText(Mascotte.FichierEtat, args[1] + "\n" + message);
                return 0;
            }
            return Lancer();
        }

        // À part de Main : un hook qui ne fait qu'envoyer un état ne charge ainsi jamais WPF.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static int Lancer()
        {
            bool premiere;
            using (new Mutex(true, "MascotteClaude-Instance", out premiere))
            {
                if (!premiere) return 0;
                var app = new Application();
                app.DispatcherUnhandledException += (s, e) =>
                {
                    try { File.AppendAllText(Path.Combine(Mascotte.Dossier, "erreurs.log"), DateTime.Now + " " + e.Exception + "\r\n"); }
                    catch (IOException) { }
                    e.Handled = true;
                };
                app.Run(new Mascotte());
            }
            return 0;
        }
    }

    sealed class Animation
    {
        public readonly int Ligne;
        public readonly int[] Colonnes;
        public readonly int[] Durees;

        public Animation(int ligne, int[] colonnes, int[] durees)
        {
            Ligne = ligne; Colonnes = colonnes; Durees = durees;
        }

        // Ligne lue de gauche à droite, la dernière image tenue un peu plus longtemps (rythme des pets Codex).
        public static Animation Suite(int ligne, int nombre, int duree, int derniere)
        {
            var colonnes = new int[nombre];
            var durees = new int[nombre];
            for (int i = 0; i < nombre; i++) { colonnes[i] = i; durees[i] = duree; }
            durees[nombre - 1] = derniere;
            return new Animation(ligne, colonnes, durees);
        }
    }

    sealed class Mascotte : Window
    {
        // Atlas : 8 colonnes x 9 lignes de cases 192x208, comme les pets Codex.
        const int CaseL = 192, CaseH = 208, Lignes = 9, Colonnes = 8;
        const double TeteCase = 68;          // haut de la tête dans la case, au repos
        const double ZoneBulle = 46, ZonePilule = 44, LargeurMin = 210;
        const double DroiteDefaut = 230, BasDefaut = 46;

        public static readonly string Dossier = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MascotteClaude");
        public static readonly string FichierEtat = Path.Combine(Dossier, "etat.txt");
        static readonly string FichierReglages = Path.Combine(Dossier, "reglages.txt");
        const string CleDemarrage = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static readonly Brush Encre = Pinceau(0x3D, 0x39, 0x29);
        static readonly Brush Creme = Pinceau(0xFA, 0xF9, 0xF5);
        static readonly Brush Trait = Pinceau(0xE4, 0xDF, 0xD0);
        static readonly Brush Survol = Pinceau(0xF0, 0xEE, 0xE6);

        readonly Animation respiration = new Animation(0, new[] { 0, 1, 2, 3 }, new[] { 620, 170, 620, 170 });
        readonly Animation clin = new Animation(0, new[] { 4 }, new[] { 140 });
        readonly Animation marcheDroite = Animation.Suite(1, 8, 120, 120);
        readonly Animation marcheGauche = Animation.Suite(2, 8, 120, 120);
        readonly Animation salut = Animation.Suite(3, 4, 140, 280);
        readonly Animation saut = Animation.Suite(4, 5, 140, 280);
        readonly Animation rate = Animation.Suite(5, 8, 140, 1400);
        readonly Animation attente = Animation.Suite(6, 6, 150, 260);
        readonly Animation travail = Animation.Suite(7, 6, 120, 220);
        readonly Animation revue = Animation.Suite(8, 6, 150, 280);

        enum Mode { Repos, Geste, Marche, Glisse, Agent }
        enum Agent { Aucun, Travail, Attente }

        readonly BitmapSource[,] images = new BitmapSource[Lignes, Colonnes];
        readonly Random hasard = new Random();
        Image sprite;
        Border bulle, pilule;
        TextBlock texteBulle;
        ContextMenu menu;

        Animation anim;
        int index, tours;
        Action fin;
        Mode mode = Mode.Repos;
        Agent agent = Agent.Aucun;

        double echelle = 0.75;
        bool balade = true, premierPlan = true;
        Point maison, ancre;             // point au sol sous la mascotte : sa place habituelle, et sa place du moment
        double cible;
        DateTime dernierPas;

        Point appuiCurseur, appuiAncre;
        bool glisse;
        int sensGlisse, clics;
        double dernierX;

        string dernierEtat;
        DateTime heureEtat;
        FileSystemWatcher guetteur;

        readonly DispatcherTimer horloge, minuteurClin, minuteurHasard, minuteurMarche, minuteurBulle, minuteurPilule, minuteurAgent;

        public Mascotte()
        {
            Title = "Mascotte Claude";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            UseLayoutRounding = true;
            WindowStartupLocation = WindowStartupLocation.Manual;

            Directory.CreateDirectory(Dossier);
            LireReglages();
            ChargerImages();
            ConstruireInterface();
            ConstruireMenu();
            Topmost = premierPlan;

            horloge = Minuteur(100, Avancer);
            minuteurClin = Minuteur(3000, Cligner);
            minuteurHasard = Minuteur(30000, (s, e) =>
            {
                // pas de fantaisie pendant qu'on s'occupe d'elle : elle ne doit pas filer sous le curseur
                if (mode == Mode.Repos && !sprite.IsMouseOver && !pilule.IsMouseOver && !menu.IsOpen) GesteAuHasard();
            });
            minuteurMarche = Minuteur(16, Pas);
            minuteurBulle = Minuteur(3000, (s, e) => CacherBulle());
            minuteurPilule = Minuteur(700, (s, e) => CacherPilule());
            // si l'état « travail » ou « attente » n'est jamais levé, on finit par revenir au repos
            minuteurAgent = Minuteur(20 * 60 * 1000, (s, e) => AppliquerEtat("idle", ""));

            Loaded += (s, e) =>
            {
                AppliquerEchelle();
                minuteurClin.Start();
                Faire(salut, 2, "Bonjour !");
                SurveillerEtat();
            };
            Closed += (s, e) => { if (guetteur != null) guetteur.Dispose(); };
        }

        // ------------------------------------------------------------------ images

        void ChargerImages()
        {
            // Un fichier mascotte.png posé à côté de l'exe remplace l'atlas intégré.
            Assembly moi = Assembly.GetExecutingAssembly();
            string externe = Path.Combine(Path.GetDirectoryName(moi.Location), "mascotte.png");
            using (Stream flux = File.Exists(externe) ? (Stream)File.OpenRead(externe) : moi.GetManifestResourceStream("atlas.png"))
            {
                var decodeur = new PngBitmapDecoder(flux, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var atlas = new FormatConvertedBitmap(decodeur.Frames[0], PixelFormats.Pbgra32, null, 0);
                int pasAtlas = atlas.PixelWidth * 4, pasCase = CaseL * 4;
                var tout = new byte[pasAtlas * atlas.PixelHeight];
                atlas.CopyPixels(tout, pasAtlas, 0);

                for (int l = 0; l < Lignes && (l + 1) * CaseH <= atlas.PixelHeight; l++)
                    for (int c = 0; c < Colonnes && (c + 1) * CaseL <= atlas.PixelWidth; c++)
                    {
                        var pixels = new byte[pasCase * CaseH];
                        for (int y = 0; y < CaseH; y++)
                            Buffer.BlockCopy(tout, (l * CaseH + y) * pasAtlas + c * pasCase, pixels, y * pasCase, pasCase);
                        var image = BitmapSource.Create(CaseL, CaseH, 96, 96, PixelFormats.Pbgra32, null, pixels, pasCase);
                        image.Freeze();
                        images[l, c] = image;
                    }
            }
            GC.Collect();                                        // l'atlas décodé ne sert plus : ~25 Mo rendus
        }

        // --------------------------------------------------------------- interface

        void ConstruireInterface()
        {
            var grille = new Grid();
            grille.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ZoneBulle) });
            grille.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grille.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ZonePilule) });

            sprite = new Image { Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Center, Cursor = Cursors.Hand };
            RenderOptions.SetBitmapScalingMode(sprite, BitmapScalingMode.HighQuality);
            Grid.SetRow(sprite, 1);
            sprite.MouseLeftButtonDown += Appui;
            sprite.MouseMove += Deplacement;
            sprite.MouseLeftButtonUp += Relache;
            sprite.MouseEnter += (s, e) => MontrerPilule();
            sprite.MouseLeave += (s, e) => minuteurPilule.Start();
            grille.Children.Add(sprite);

            texteBulle = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"), FontSize = 12.5, Foreground = Encre,
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = LargeurMin - 36
            };
            bulle = new Border
            {
                Background = Creme, BorderBrush = Trait, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11),
                Padding = new Thickness(10, 5, 10, 6), Child = texteBulle, Effect = Ombre(),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
                Opacity = 0, IsHitTestVisible = false
            };
            Grid.SetRow(bulle, 0);
            grille.Children.Add(bulle);

            // La pilule qui apparaît sous la mascotte au survol, comme celle de Codex.
            var boutons = new StackPanel { Orientation = Orientation.Horizontal };
            boutons.Children.Add(Bouton("", "Nouvelle conversation avec Claude", OuvrirClaude));
            boutons.Children.Add(new Border { Width = 1, Height = 16, Background = Trait, Margin = new Thickness(3, 0, 3, 0) });
            boutons.Children.Add(Bouton("", "Menu de la mascotte", OuvrirMenu));
            pilule = new Border
            {
                Background = Brushes.White, CornerRadius = new CornerRadius(17), Padding = new Thickness(5, 3, 5, 3),
                Child = boutons, Effect = Ombre(), Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                Opacity = 0, IsHitTestVisible = false
            };
            Grid.SetRow(pilule, 2);
            pilule.MouseEnter += (s, e) => MontrerPilule();
            pilule.MouseLeave += (s, e) => minuteurPilule.Start();
            grille.Children.Add(pilule);

            Content = grille;
        }

        Border Bouton(string glyphe, string aide, Action clic)
        {
            var bouton = new Border
            {
                Width = 32, Height = 28, CornerRadius = new CornerRadius(14), Background = Brushes.Transparent,
                Cursor = Cursors.Hand, ToolTip = aide,
                Child = new TextBlock
                {
                    Text = glyphe, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 15,
                    Foreground = Encre, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            };
            bouton.MouseEnter += (s, e) => bouton.Background = Survol;
            bouton.MouseLeave += (s, e) => bouton.Background = Brushes.Transparent;
            bouton.MouseLeftButtonUp += (s, e) => { clic(); e.Handled = true; };
            return bouton;
        }

        void ConstruireMenu()
        {
            menu = new ContextMenu();
            menu.Items.Add(Element("Nouvelle conversation avec Claude", OuvrirClaude));
            menu.Items.Add(new Separator());

            var animations = new MenuItem { Header = "Animations" };
            animations.Items.Add(Element("Saluer", () => Faire(salut, 2, null)));
            animations.Items.Add(Element("Sauter", () => Faire(saut, 1, null)));
            animations.Items.Add(Element("Se promener", Balade));
            animations.Items.Add(Element("Travailler", () => Faire(travail, 5, null)));
            animations.Items.Add(Element("Attendre une réponse", () => Faire(attente, 3, null)));
            animations.Items.Add(Element("Vérifier", () => Faire(revue, 2, null)));
            animations.Items.Add(Element("Raté", () => Faire(rate, 1, null)));
            menu.Items.Add(animations);

            var taille = new MenuItem { Header = "Taille" };
            var tailles = new[] { "Petite", "Moyenne", "Grande", "Très grande" };
            var valeurs = new[] { 0.5, 0.75, 1.0, 1.5 };
            for (int i = 0; i < tailles.Length; i++)
            {
                double valeur = valeurs[i];
                var choix = new MenuItem { Header = tailles[i], IsCheckable = true, IsChecked = Math.Abs(echelle - valeur) < 0.01 };
                choix.Click += (s, e) =>
                {
                    foreach (MenuItem autre in taille.Items) autre.IsChecked = autre == choix;
                    echelle = valeur;
                    AppliquerEchelle();
                    Enregistrer();
                };
                taille.Items.Add(choix);
            }
            menu.Items.Add(taille);

            menu.Items.Add(Coche("Se balader de temps en temps", balade, v => { balade = v; Enregistrer(); }));
            menu.Items.Add(Coche("Toujours au premier plan", premierPlan, v => { premierPlan = v; Topmost = v; Enregistrer(); }));
            menu.Items.Add(Coche("Lancer au démarrage de Windows", LanceAuDemarrage(), DefinirDemarrage));
            menu.Items.Add(Element("Revenir dans le coin", () =>
            {
                maison = PlaceParDefaut();
                ancre = maison;
                Placer();
                Enregistrer();
            }));
            menu.Items.Add(new Separator());
            menu.Items.Add(Element("Quitter", Close));
            sprite.ContextMenu = menu;
        }

        static MenuItem Element(string titre, Action action)
        {
            var element = new MenuItem { Header = titre };
            element.Click += (s, e) => action();
            return element;
        }

        static MenuItem Coche(string titre, bool cochee, Action<bool> action)
        {
            var element = new MenuItem { Header = titre, IsCheckable = true, IsChecked = cochee };
            element.Click += (s, e) => action(element.IsChecked);
            return element;
        }

        void OuvrirMenu()
        {
            menu.PlacementTarget = pilule;
            menu.Placement = PlacementMode.Top;
            menu.IsOpen = true;
        }

        void MontrerPilule()
        {
            minuteurPilule.Stop();
            pilule.IsHitTestVisible = true;
            Fondu(pilule, 1);
        }

        void CacherPilule()
        {
            minuteurPilule.Stop();
            if (menu.IsOpen || sprite.IsMouseOver || pilule.IsMouseOver) return;
            pilule.IsHitTestVisible = false;
            Fondu(pilule, 0);
        }

        void Dire(string texte, double secondes)
        {
            texteBulle.Text = texte;
            Fondu(bulle, 1);
            minuteurBulle.Stop();
            if (secondes > 0)
            {
                minuteurBulle.Interval = TimeSpan.FromSeconds(secondes);
                minuteurBulle.Start();
            }
        }

        void CacherBulle()
        {
            minuteurBulle.Stop();
            Fondu(bulle, 0);
        }

        static void Fondu(UIElement element, double opacite)
        {
            element.BeginAnimation(OpacityProperty, new DoubleAnimation(opacite, TimeSpan.FromMilliseconds(160)));
        }

        static DropShadowEffect Ombre()
        {
            return new DropShadowEffect { BlurRadius = 9, ShadowDepth = 1, Direction = 270, Opacity = 0.28, Color = Colors.Black };
        }

        static Brush Pinceau(byte r, byte v, byte b)
        {
            var pinceau = new SolidColorBrush(Color.FromRgb(r, v, b));
            pinceau.Freeze();
            return pinceau;
        }

        static DispatcherTimer Minuteur(double millisecondes, EventHandler tic)
        {
            var minuteur = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(millisecondes) };
            minuteur.Tick += tic;
            return minuteur;
        }

        // ------------------------------------------------------- place sur l'écran

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // fenêtre-outil : absente d'Alt+Tab
            IntPtr poignee = new WindowInteropHelper(this).Handle;
            SetWindowLong(poignee, GWL_EXSTYLE, GetWindowLong(poignee, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
        }

        static Point PlaceParDefaut()
        {
            Rect zone = SystemParameters.WorkArea;
            return new Point(zone.Right - DroiteDefaut, zone.Bottom - BasDefaut);
        }

        void AppliquerEchelle()
        {
            sprite.Width = CaseL * echelle;
            sprite.Height = CaseH * echelle;
            Width = Math.Max(CaseL * echelle, LargeurMin);
            Height = ZoneBulle + CaseH * echelle + ZonePilule;
            // la bulle descend dans le vide de la case, juste au-dessus de la tête
            bulle.Margin = new Thickness(0, 0, 0, -(TeteCase * echelle - 8));
            Placer();
        }

        void Placer()
        {
            Left = ancre.X - Width / 2;
            Top = ancre.Y - (ZoneBulle + CaseH * echelle);
        }

        void Borner()
        {
            double gauche = SystemParameters.VirtualScreenLeft, haut = SystemParameters.VirtualScreenTop;
            ancre.X = Math.Max(gauche + 40, Math.Min(gauche + SystemParameters.VirtualScreenWidth - 40, ancre.X));
            ancre.Y = Math.Max(haut + 80, Math.Min(haut + SystemParameters.VirtualScreenHeight - 10, ancre.Y));
        }

        Point Curseur()
        {
            POINT p;
            GetCursorPos(out p);
            PresentationSource source = PresentationSource.FromVisual(this);
            Matrix versUnites = source != null ? source.CompositionTarget.TransformFromDevice : Matrix.Identity;
            return versUnites.Transform(new Point(p.X, p.Y));
        }

        // -------------------------------------------------------------- animation

        void Jouer(Animation animation, int nombreDeTours, Action aLaFin)   // 0 tour = en boucle
        {
            anim = animation;
            index = 0;
            tours = nombreDeTours;
            fin = aLaFin;
            Afficher();
        }

        void Afficher()
        {
            sprite.Source = images[anim.Ligne, anim.Colonnes[index]];
            horloge.Stop();
            horloge.Interval = TimeSpan.FromMilliseconds(anim.Durees[index]);
            horloge.Start();
        }

        void Avancer(object s, EventArgs e)
        {
            index++;
            if (index >= anim.Colonnes.Length)
            {
                index = 0;
                if (tours > 0 && --tours == 0)
                {
                    horloge.Stop();
                    Action suite = fin;
                    fin = null;
                    if (suite != null) suite(); else Reprendre();
                    return;
                }
            }
            Afficher();
        }

        // Retour à l'activité de fond : ce que fait Claude Code si on le sait, sinon le repos.
        void Reprendre()
        {
            if (agent == Agent.Travail) { mode = Mode.Agent; Jouer(travail, 0, null); }
            else if (agent == Agent.Attente) { mode = Mode.Agent; Jouer(attente, 0, null); }
            else
            {
                mode = Mode.Repos;
                Jouer(respiration, 0, null);
                minuteurHasard.Stop();
                minuteurHasard.Interval = TimeSpan.FromSeconds(20 + hasard.Next(30));
                minuteurHasard.Start();
            }
        }

        void Faire(Animation animation, int nombreDeTours, string parole)
        {
            mode = Mode.Geste;
            minuteurHasard.Stop();
            Jouer(animation, nombreDeTours, Reprendre);
            if (parole != null) Dire(parole, 3);
        }

        void Cligner(object s, EventArgs e)
        {
            minuteurClin.Interval = TimeSpan.FromMilliseconds(2500 + hasard.Next(4000));
            if (mode == Mode.Repos && anim == respiration)
                Jouer(clin, 1, () => Jouer(respiration, 0, null));
        }

        void GesteAuHasard()
        {
            double tirage = hasard.NextDouble();
            if (balade && tirage < 0.45) Balade();
            else if (tirage < 0.65) Faire(salut, 2, null);
            else if (tirage < 0.85) Faire(saut, 1, null);
            else Faire(revue, 2, null);
        }

        // ------------------------------------------------------------------ marche

        void Balade()
        {
            double gauche = SystemParameters.VirtualScreenLeft + Width / 2;
            double droite = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width / 2;
            double but;
            if (Math.Abs(ancre.X - maison.X) > 20 && hasard.NextDouble() < 0.6)
                but = maison.X;                                  // le plus souvent, elle rentre chez elle
            else
            {
                double distance = 80 + hasard.NextDouble() * 180;
                if (hasard.Next(2) == 0) distance = -distance;
                but = Math.Max(gauche, Math.Min(droite, maison.X + distance));
                if (Math.Abs(but - ancre.X) < 40)                // coincée contre un bord : partir de l'autre côté
                    but = Math.Max(gauche, Math.Min(droite, maison.X - distance));
            }
            if (Math.Abs(but - ancre.X) < 10) { Reprendre(); return; }

            mode = Mode.Marche;
            minuteurHasard.Stop();
            cible = but;
            Jouer(cible > ancre.X ? marcheDroite : marcheGauche, 0, null);
            dernierPas = DateTime.UtcNow;
            minuteurMarche.Start();
        }

        void Pas(object s, EventArgs e)
        {
            if (mode != Mode.Marche) { minuteurMarche.Stop(); return; }
            DateTime maintenant = DateTime.UtcNow;
            double pas = 70 * echelle * Math.Min(0.1, (maintenant - dernierPas).TotalSeconds);
            dernierPas = maintenant;
            if (Math.Abs(cible - ancre.X) <= pas)
            {
                ancre.X = cible;
                Placer();
                minuteurMarche.Stop();
                Reprendre();
                return;
            }
            ancre.X += cible > ancre.X ? pas : -pas;
            Placer();
        }

        // ------------------------------------------------------------------ souris

        void Appui(object s, MouseButtonEventArgs e)
        {
            appuiCurseur = Curseur();
            appuiAncre = ancre;
            glisse = false;
            sprite.CaptureMouse();
        }

        void Deplacement(object s, MouseEventArgs e)
        {
            if (!sprite.IsMouseCaptured) return;
            Point curseur = Curseur();
            double dx = curseur.X - appuiCurseur.X, dy = curseur.Y - appuiCurseur.Y;
            if (!glisse)
            {
                if (Math.Abs(dx) + Math.Abs(dy) < 5) return;     // simple clic qui tremble
                glisse = true;
                mode = Mode.Glisse;
                minuteurHasard.Stop();
                sensGlisse = 0;
                dernierX = appuiCurseur.X;
            }
            ancre = new Point(appuiAncre.X + dx, appuiAncre.Y + dy);
            Borner();
            Placer();

            // elle court dans le sens où on la tire
            double vitesse = curseur.X - dernierX;
            if (sensGlisse == 0 || Math.Abs(vitesse) >= 3)
            {
                int sens = vitesse < 0 ? -1 : 1;
                if (sens != sensGlisse)
                {
                    sensGlisse = sens;
                    Jouer(sens > 0 ? marcheDroite : marcheGauche, 0, null);
                }
                dernierX = curseur.X;
            }
        }

        void Relache(object s, MouseButtonEventArgs e)
        {
            if (!sprite.IsMouseCaptured) return;
            sprite.ReleaseMouseCapture();
            if (glisse)
            {
                maison = ancre;
                Enregistrer();
                Reprendre();
            }
            else if (++clics % 3 == 0) Faire(saut, 1, null);
            else Faire(salut, 2, null);
        }

        void OuvrirClaude()
        {
            // l'appli de bureau Claude si elle est installée (protocole claude://), sinon le site
            bool appli;
            using (RegistryKey cle = Registry.ClassesRoot.OpenSubKey("claude")) appli = cle != null;
            try { Process.Start(appli ? "claude://claude.ai/new" : "https://claude.ai/new"); }
            catch (Exception) { Dire("Impossible d'ouvrir Claude", 3); }
        }

        // ------------------------------------------- états envoyés par Claude Code

        void SurveillerEtat()
        {
            guetteur = new FileSystemWatcher(Dossier, Path.GetFileName(FichierEtat));
            guetteur.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size;
            guetteur.Changed += EtatModifie;
            guetteur.Created += EtatModifie;
            guetteur.EnableRaisingEvents = true;
        }

        void EtatModifie(object s, FileSystemEventArgs e)
        {
            // appelé hors du fil de l'interface ; le fichier peut encore être tenu par celui qui l'écrit
            for (int essai = 0; essai < 6; essai++)
            {
                try
                {
                    string[] lignes = File.ReadAllLines(FichierEtat);
                    if (lignes.Length == 0) return;
                    string etat = lignes[0].Trim().ToLowerInvariant();
                    string message = lignes.Length > 1 ? lignes[1].Trim() : "";
                    Dispatcher.BeginInvoke(new Action(() => AppliquerEtat(etat, message)));
                    return;
                }
                catch (IOException) { Thread.Sleep(40); }
            }
        }

        void AppliquerEtat(string etat, string message)
        {
            // le guetteur signale souvent deux fois la même écriture
            string empreinte = etat + "\n" + message;
            if (empreinte == dernierEtat && (DateTime.UtcNow - heureEtat).TotalMilliseconds < 500) return;
            dernierEtat = empreinte;
            heureEtat = DateTime.UtcNow;

            bool muet = message.Length == 0;
            minuteurAgent.Stop();
            switch (etat)
            {
                case "running": case "travail":
                    minuteurAgent.Start();
                    // un hook renvoie « running » après chaque outil : on ne recommence ni la bulle ni l'animation
                    if (agent == Agent.Travail && muet) return;
                    agent = Agent.Travail;
                    Dire(muet ? "Je m'en occupe…" : message, 4);
                    break;
                case "waiting": case "attente":
                    minuteurAgent.Start();
                    if (agent == Agent.Attente && muet) return;
                    agent = Agent.Attente;
                    Dire(muet ? "J'ai besoin de toi !" : message, 0);
                    break;
                case "review": case "fini":
                    agent = Agent.Aucun;
                    Dire(muet ? "C'est prêt !" : message, 6);
                    if (mode != Mode.Glisse) { mode = Mode.Geste; minuteurHasard.Stop(); Jouer(revue, 2, () => Faire(salut, 2, null)); }
                    return;
                case "failed": case "erreur":
                    agent = Agent.Aucun;
                    Dire(muet ? "Aïe, ça a coincé…" : message, 6);
                    if (mode != Mode.Glisse) Faire(rate, 1, null);
                    return;
                case "waving": case "salut":
                    if (mode != Mode.Glisse) Faire(salut, 2, muet ? null : message);
                    return;
                case "jumping": case "saut":
                    if (mode != Mode.Glisse) Faire(saut, 1, muet ? null : message);
                    return;
                case "walking": case "balade":
                    if (mode != Mode.Glisse) Balade();
                    return;
                default:                                         // idle, repos, ou état inconnu
                    agent = Agent.Aucun;
                    if (muet) CacherBulle(); else Dire(message, 4);
                    break;
            }
            if (mode != Mode.Glisse) Reprendre();
        }

        // ---------------------------------------------------------------- réglages

        void LireReglages()
        {
            double droite = DroiteDefaut, bas = BasDefaut;
            if (File.Exists(FichierReglages))
                foreach (string ligne in File.ReadAllLines(FichierReglages))
                {
                    string[] morceaux = ligne.Split('=');
                    double nombre;
                    if (morceaux.Length != 2 || !double.TryParse(morceaux[1], NumberStyles.Float, CultureInfo.InvariantCulture, out nombre)) continue;
                    switch (morceaux[0])
                    {
                        case "echelle": echelle = Math.Max(0.3, Math.Min(3, nombre)); break;
                        case "droite": droite = nombre; break;
                        case "bas": bas = nombre; break;
                        case "balade": balade = nombre != 0; break;
                        case "premierplan": premierPlan = nombre != 0; break;
                    }
                }
            // la place est retenue par rapport au coin bas-droit de l'écran, là où elle vit d'habitude
            Rect zone = SystemParameters.WorkArea;
            ancre = new Point(zone.Right - droite, zone.Bottom - bas);
            Borner();
            maison = ancre;
        }

        void Enregistrer()
        {
            Rect zone = SystemParameters.WorkArea;
            CultureInfo c = CultureInfo.InvariantCulture;
            File.WriteAllLines(FichierReglages, new[]
            {
                "echelle=" + echelle.ToString(c),
                "droite=" + (zone.Right - maison.X).ToString("0.#", c),
                "bas=" + (zone.Bottom - maison.Y).ToString("0.#", c),
                "balade=" + (balade ? "1" : "0"),
                "premierplan=" + (premierPlan ? "1" : "0")
            });
        }

        static bool LanceAuDemarrage()
        {
            using (RegistryKey cle = Registry.CurrentUser.OpenSubKey(CleDemarrage))
                return cle != null && cle.GetValue("MascotteClaude") != null;
        }

        static void DefinirDemarrage(bool actif)
        {
            using (RegistryKey cle = Registry.CurrentUser.OpenSubKey(CleDemarrage, true))
            {
                if (cle == null) return;
                if (actif) cle.SetValue("MascotteClaude", "\"" + Assembly.GetExecutingAssembly().Location + "\"");
                else cle.DeleteValue("MascotteClaude", false);
            }
        }

        // ------------------------------------------------------------------- Win32

        const int GWL_EXSTYLE = -20, WS_EX_TOOLWINDOW = 0x80;

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr fenetre, int index);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr fenetre, int index, int valeur);
    }
}
