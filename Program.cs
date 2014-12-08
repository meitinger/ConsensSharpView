/* Copyright (C) 2012, Manuel Meitinger
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 2 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Consens SharpView")]
[assembly: AssemblyDescription(".NET port of Consens WebView")]
[assembly: AssemblyCompany("Aufbauwerk der Jugend")]
[assembly: AssemblyProduct("Consens SharpView")]
[assembly: AssemblyCopyright("Copyright © 2012 by Aufbauwerk der Jugend")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.1.0.4")]

namespace Aufbauwerk.Tools.ConsensSharpView
{
    static class Splash
    {
        static Form form;

        [STAThread]
        static void Main(string[] args)
        {
            // we like pretty colors and shiny objects
            Application.EnableVisualStyles();

            // create the splash screen components
            var transparentColor = Color.FromArgb(1, 1, 1);
            var size = new Size(72, 72);
            form = new Form() { FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, TransparencyKey = transparentColor, BackColor = transparentColor, MinimumSize = size, ClientSize = size, StartPosition = FormStartPosition.CenterScreen, TopMost = true, };
            var pictureBox = new PictureBox() { Dock = DockStyle.Top, Height = 60, SizeMode = PictureBoxSizeMode.CenterImage, };
            var progressBar = new ProgressBar() { Dock = DockStyle.Bottom, Height = size.Height - pictureBox.Height, Step = 1, Maximum = 16, Style = ProgressBarStyle.Continuous, };

            // layout the splash screen and load the splash image
            form.SuspendLayout();
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Splash).Namespace + ".IconLarge.png"))
                pictureBox.Image = Image.FromStream(stream);
            form.Controls.Add(pictureBox);
            form.Controls.Add(progressBar);
            form.ResumeLayout(true);

            // reflect assembly loading with progress stepping
            var performStep = new MethodInvoker(() => progressBar.PerformStep());
            AppDomain.CurrentDomain.AssemblyLoad += (o, arg) => form.Invoke(performStep);

            // defer java initialization work to a different thread
            ThreadPool.QueueUserWorkItem(state =>
            {
                try { MainFrame.Initialize(args, () => form.Invoke(performStep)); }
                finally { Close(); }
            });

            // show the splash and pump messages
            Application.Run(form);
        }

        internal static void Close()
        {
            // close the form
            try { if (!form.IsDisposed) form.BeginInvoke(new MethodInvoker(() => { if (!form.IsDisposed) form.Close(); })); }
            catch { }
        }
    }

    static class Extensions
    {
        public static Form getForm(this javax.swing.JFrame frame)
        {
            // retreive the WinForm from a java frame
            var peer = frame.getPeer();
            return (Form)peer.GetType().GetProperty("Control", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(peer, null);
        }
    }

    class Applet : com.ulcjava.environment.applet.client.DefaultAppletLauncher
    {
        readonly MainFrame program;

        public Applet(MainFrame program)
        {
            // create the applet and attach its content to the main frame
            this.program = program;
            program.setContentPane(getContentPane());
        }

        public override void sessionEnded(com.ulcjava.@base.client.UISession uis)
        {
            // exit on session end
            base.sessionEnded(uis);
            java.lang.System.exit(0);
        }

        public override void setContentPane(java.awt.Container contentPane)
        {
            // hide the main frame, add the new content to it and show it again
            base.setContentPane(contentPane);
            program.setVisible(false);
            program.setContentPane(contentPane);
            program.setVisible(true);
            program.toFront();
        }

        public override void sessionError(com.ulcjava.@base.client.UISession uis, Exception t)
        {
            // hide the splash dialog if necessary, show the error dialog and exit
            Splash.Close();
            base.sessionError(uis, t);
            java.lang.System.exit(Marshal.GetHRForException(t));
        }
    }

    static class Credentials
    {
        const string RegRootKey = @"HKEY_CURRENT_USER\Software\Consens";
        const string RegUserNameValue = "Username";
        const string RegPasswordValue = "Password";

        public static string UserName { get; set; }
        public static string Password { get; set; }
        public static bool Save { get; set; }

        static Credentials()
        {
            // load the credentials from the registry or use the current username and an empty password as default
            Save = false;
            Password = string.Empty;
            UserName = Registry.GetValue(RegRootKey, RegUserNameValue, null) as string;
            if (!string.IsNullOrEmpty(UserName))
            {
                var regPassword = Registry.GetValue(RegRootKey, RegPasswordValue, null) as byte[];
                if (regPassword != null && regPassword.Length > 0)
                {
                    Save = true;
                    try { Password = Encoding.Unicode.GetString(ProtectedData.Unprotect(regPassword, null, DataProtectionScope.CurrentUser)); }
                    catch { Save = false; }
                }
            }
            else
                UserName = Environment.UserName;
        }

        public static void Update()
        {
            // persist the credentials to the registry
            Registry.SetValue(RegRootKey, RegUserNameValue, UserName);
            Registry.SetValue(RegRootKey, RegPasswordValue, Save ? ProtectedData.Protect(Encoding.Unicode.GetBytes(Password), null, DataProtectionScope.CurrentUser) : new byte[] { });
        }
    }

    class LogonDialog : java.awt.@event.ActionListener, java.beans.PropertyChangeListener
    {
        readonly javax.swing.JTextField userName;
        readonly javax.swing.JPasswordField password;
        readonly javax.swing.JCheckBox saveCredentials;

        LogonDialog(javax.swing.JTextField userName, javax.swing.JPasswordField password, javax.swing.JCheckBox saveCredentials)
        {
            this.userName = userName;
            this.password = password;
            this.saveCredentials = saveCredentials;
        }

        public static bool match(javax.swing.JFrame frame)
        {
            // test if the given frame is the logon dialog
            return frame.getTitle().StartsWith("ZeitConsens Webinterface");
        }

        public static void wrap(Form owner, javax.swing.JFrame frame)
        {
            // fetch the required gui elements
            var panelOuter = (javax.swing.JPanel)frame.getContentPane().getComponent(0);
            var panelInner = (javax.swing.JPanel)panelOuter.getComponent(0);
            var userNameInput = (javax.swing.JTextField)panelInner.getComponent(2);
            var passwordInput = (javax.swing.JPasswordField)panelInner.getComponent(4);
            var okButton = (javax.swing.JButton)panelInner.getComponent(7);

            // update the dialog title and input boxes
            frame.setTitle("Consens-Anmeldung");
            userNameInput.setText(Credentials.UserName);
            passwordInput.setText(Credentials.Password);

            // create a checkbox for specifying whether to persist the credentials
            var saveCredentialsInput = new javax.swing.JCheckBox("Kennwort speichern", Credentials.Save);
            var c = new java.awt.GridBagConstraints();
            c.fill = java.awt.GridBagConstraints.HORIZONTAL;
            c.gridx = 4;
            c.gridy = 4;
            panelInner.add(saveCredentialsInput, c);
            frame.validate();

            // hook the required events
            var dialog = new LogonDialog(userNameInput, passwordInput, saveCredentialsInput);
            passwordInput.addPropertyChangeListener("enabled", dialog);
            okButton.addActionListener(dialog);

            // turn the WinForm into a dialog
            var form = frame.getForm();
            form.BeginInvoke(new MethodInvoker(() =>
            {
                form.Owner = owner;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.Focus();
            }));
        }

        public void actionPerformed(java.awt.@event.ActionEvent ae)
        {
            // update the credentials
            Credentials.UserName = userName.getText();
            Credentials.Password = password.getText();
            Credentials.Save = saveCredentials.isSelected();
            Credentials.Update();
        }

        public void propertyChange(java.beans.PropertyChangeEvent pce)
        {
            // disable the checkbox once the logon completes
            saveCredentials.setEnabled(false);
        }
    }

    class MainFrame : javax.swing.JFrame, java.applet.AppletStub, java.applet.AppletContext, java.awt.@event.AWTEventListener
    {
        readonly java.net.URL host;

        MainFrame(java.net.URL host)
        {
            // create the main frame and its components
            this.host = host;

            // hook window open events and flag the frame as exit-on-close
            getToolkit().addAWTEventListener(this, java.awt.AWTEvent.WINDOW_EVENT_MASK);
            setDefaultCloseOperation(javax.swing.JFrame.EXIT_ON_CLOSE);

            // set the title and frame icons
            setTitle(Application.ProductName);
            setIconImages(new java.util.ArrayList() { getImage("IconLarge.png"), getImage("IconSmall.png") });

            // position and size the frame appropriately
            var screenSize = getToolkit().getScreenSize();
            setSize((int)(screenSize.width * 0.75), (int)(screenSize.height * 0.75));
            setLocationRelativeTo(null);

            // create and initialize the Consens applet
            var applet = new Applet(this);
            applet.setStub(this);
            applet.init();
            applet.start();
        }

        public void eventDispatched(java.awt.AWTEvent awte)
        {
            // support modifications to the logon dialog
            if (awte.getID() == java.awt.@event.WindowEvent.WINDOW_OPENED)
            {
                var frame = awte.getSource() as javax.swing.JFrame;
                if (frame != null && LogonDialog.match(frame))
                    LogonDialog.wrap(this.getForm(), frame);
            }
        }

        public java.awt.Image getImage(string name)
        {
            // load and return a resource image
            using (var stream = GetType().Assembly.GetManifestResourceStream(GetType().Namespace + "." + name))
            using (var buffer = new MemoryStream())
            {
                byte[] chunk = new byte[4000];
                int read;
                while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                    buffer.Write(chunk, 0, read);
                buffer.Flush();
                return getToolkit().createImage(buffer.ToArray());
            }
        }

        public void appletResize(int i1, int i2)
        {
            // grant the resize wish to the applet
            setSize(i1, i2);
        }

        public java.applet.AppletContext getAppletContext()
        {
            // return the main frame as applet context
            return this;
        }

        public java.net.URL getCodeBase()
        {
            // return the relative code base path, as specified within the <APPLET> element
            return new java.net.URL(".");
        }

        public java.net.URL getDocumentBase()
        {
            // return the absolute document base path, as specified within the <APPLET> element
            return new java.net.URL(host, "/zcwebview/webview_applet.jsp");
        }

        public string getParameter(string str)
        {
            // return all the parameters that would otherwise be specified within the <APPLET> element, but with auto-logon
            switch (str.ToLowerInvariant())
            {
                case "keep-alive-interval":
                    return "900";
                case "log-level":
                    return "WARNING";
                case "url-string":
                    return new java.net.URL(
                        host,
                        Credentials.Save && (Control.ModifierKeys & Keys.Shift) == 0 ?
                            string.Format("/zcwebview/webview-applet.ulc?tagid=null&user={0}&pass={1}&view=null", Uri.EscapeDataString(Credentials.UserName), Uri.EscapeDataString(Credentials.Password))
                        :
                            "/zcwebview/webview-applet.ulc?tagid=null&user=null&pass=null&view=null"
                    ).toString();
                default:
                    return null;
            }
        }

        public void showDocument(java.net.URL url, string str)
        {
            // naming a browser window is not supported, so just show the document
            showDocument(url);
        }

        public void showDocument(java.net.URL url)
        {
            // open the given document in the default browser
            java.awt.Desktop.getDesktop().browse(url.toURI());
        }

        public static void Initialize(string[] args, Action madeProgress)
        {
            // enable the native look & feel, create the main frame - either for a given host or the local host - and show it
            madeProgress();
            javax.swing.UIManager.setLookAndFeel(javax.swing.UIManager.getSystemLookAndFeelClassName());
            string server;
            if (args.Length > 0)
            {
                server = args[0];
                if (args.Length > 1)
                {
                    Credentials.UserName = args[1];
                    Credentials.Password = (Credentials.Save = args.Length > 2) ? args[2] : string.Empty;
                }
            }
            else server = "http://127.0.0.1:8080";
            var program = new MainFrame(new java.net.URL(server));
            madeProgress();
            program.setVisible(true);
            madeProgress();
            javax.swing.SwingUtilities.invokeAndWait(ikvm.runtime.Delegates.toRunnable(() => program.toFront()));
        }

        #region Not implemented methods.

        public java.applet.Applet getApplet(string str) { throw new NotImplementedException(); }
        public java.util.Enumeration getApplets() { throw new NotImplementedException(); }
        public java.applet.AudioClip getAudioClip(java.net.URL url) { throw new NotImplementedException(); }
        public java.awt.Image getImage(java.net.URL url) { throw new NotImplementedException(); }
        public java.io.InputStream getStream(string str) { throw new NotImplementedException(); }
        public java.util.Iterator getStreamKeys() { throw new NotImplementedException(); }
        public void setStream(string str, java.io.InputStream @is) { throw new NotImplementedException(); }
        public void showStatus(string str) { throw new NotImplementedException(); }

        #endregion

    }
}
