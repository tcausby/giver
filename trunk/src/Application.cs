//***********************************************************************
// *  Application.cs
// *
// *  Copyright (C) 2007 Novell, Inc.
// *
// *  This program is free software; you can redistribute it and/or
// *  modify it under the terms of the GNU General Public
// *  License as published by the Free Software Foundation; either
// *  version 2 of the License, or (at your option) any later version.
// *
// *  This program is distributed in the hope that it will be useful,
// *  but WITHOUT ANY WARRANTY; without even the implied warranty of
// *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// *  General Public License for more details.
// *
// *  You should have received a copy of the GNU General Public
// *  License along with this program; if not, write to the Free
// *  Software Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
// *
// **********************************************************************

using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using System.Net.Sockets;

using Gtk;
using Gdk;
using Gnome;
using Mono.Unix;
using Mono.Unix.Native;
using Notifications;

namespace Giver
{


	class Application
	{
		#region Private Static Types
		private static Giver.Application application = null;
		private static System.Object locker = new System.Object();
		#endregion
		
		#region Private Types
        private Gnome.Program program;
		private Gdk.Pixbuf onPixBuf;
		private Gdk.Pixbuf offPixBuf;
		private Gtk.Image trayImage;
		private Egg.TrayIcon trayIcon;	
		private GiverService service;
		private ServiceLocator locator;
		private RequestHandler requestHandler;
		private SendingHandler sendingHandler;
		private Preferences preferences;
		#endregion
	
		#region Public Static Properties
		public static Application Instance
		{
			get
			{
				lock(locker)
				{
					if(application == null)
					{
						lock(locker)
						{
							application = new Application();
						}
					}
					return application;
				}
			}
		}

		public static Preferences Preferences
		{
			get { return Application.Instance.preferences; }
		}
		#endregion

		#region Constructors	
		private Application ()
		{
			Init(null);
		}
	
		private Application (string[] args)
		{
			Init(args);
		}
		#endregion


		#region Private Methods
		private void Init(string[] args)
		{
//			Gtk.Application.Init ();

			program = 
				new Gnome.Program (
						"Giver",
						Defines.Version,
						Gnome.Modules.UI,
						args);

			preferences = new Preferences();

			GLib.Idle.Add(InitializeIdle);
		}
	
		private bool InitializeIdle()
		{
			requestHandler = new RequestHandler();
			sendingHandler = new SendingHandler();
			sendingHandler.Start();

			try {
				locator = new ServiceLocator();
			} catch (Exception e) {
				if(e.Message.CompareTo("Daemon not running") == 0) {
					Logger.Fatal("The Avahi Daemon is not running... start it before running Giver");
				}
				else
					Logger.Debug("Error starting ServiceLocator: {0}", e.Message);

				throw e;
			}
			try {
				service = new GiverService();
			} catch (Exception e) {
				if(e.Message.CompareTo("Daemon not running") == 0) {
					Logger.Fatal("The Avahi Daemon is not running... start it before running Giver");
				}
				else
					Logger.Debug("Error starting ServiceLocator: {0}", e.Message);

				throw e;
			}

			locator.ServiceRemoved += OnServicesChanged;
			locator.ServiceAdded += OnServicesChanged;
			service.ClientConnected += OnClientConnected;

			//tray = new NotificationArea("RtcApplication");
			SetupTrayIcon();

			TargetWindow.ShowWindow(locator);

			return false;
		}
  
		private void UpdateTrayIcon()
		{
			if(locator.Count > 0)
				trayImage.Pixbuf = onPixBuf;
			else
				trayImage.Pixbuf = offPixBuf;
		}
 
	 	private void OnServicesChanged (object o, ServiceArgs args)
		{
			Gtk.Application.Invoke( delegate {
				UpdateTrayIcon();
			} );
		}


		private void OnClientConnected (HttpListenerContext context)
		{
			requestHandler.HandleRequest(context);
		}


		private void SetupTrayIcon ()
		{
//			Logger.Debug ("Creating TrayIcon");
			
			EventBox eb = new EventBox();
			onPixBuf = Utilities.GetIcon ("giver-24", 24);
			offPixBuf = Utilities.GetIcon ("giveroff-24", 24);
			if(locator.Count > 0)
				trayImage = new Gtk.Image(onPixBuf);
			else
				trayImage = new Gtk.Image(offPixBuf);
			eb.Add(trayImage);
			//new Image(Gtk.Stock.DialogWarning, IconSize.Menu)); // using stock icon



			// hooking event
			eb.ButtonPressEvent += OnTrayIconClick;
			trayIcon = new Egg.TrayIcon("Giver");
			trayIcon.Add(eb); 

			trayIcon.EnterNotifyEvent += OnNotifyEvent;
			// showing the trayicon
			trayIcon.ShowAll();			
		}

		private void OnNotifyEvent(object o, EventArgs args)
		{
		}

		private void OnPreferences (object sender, EventArgs args)
		{
			Logger.Info ("OnPreferences called");
			Giver.PreferencesDialog dialog = new PreferencesDialog();
			dialog.Run();
			dialog.Hide();
			dialog.Destroy();
	
		}

		private void OnAbout (object sender, EventArgs args)
		{
            string [] authors = new string [] {
                "Calvin Gaisford <calvinrg@gmail.com>",
                "Scott Reeves <sreeves@gmail.com>",
                "Travis Hansen <thansen@novell.com>"
            };

           /* string [] documenters = new string [] {
                "Calvin Gaisford <calvinrg@gmail.com>"
            };

			string translators = Catalog.GetString ("translator-credits");
            if (translators == "translator-credits")
                translators = null;
			*/

            Gtk.AboutDialog about = new Gtk.AboutDialog ();
            about.Name = "Giver";
            about.Version = Defines.Version;
            about.Logo = Utilities.GetIcon("giver-48", 48);
            about.Copyright =
                Catalog.GetString ("Copyright \xa9 2007 Calvin Gaisford");
            about.Comments = Catalog.GetString ("A simple and easy to use desktop " +
                                "file-sharing application.");
            about.Website = "http://idea.opensuse.org/content/ideas/easy-file-sharing";
            about.WebsiteLabel = Catalog.GetString("Homepage");
            about.Authors = authors;
            //about.Documenters = documenters;
            //about.TranslatorCredits = translators;
            about.IconName = "giver";
            about.Run ();
            about.Destroy ();
		}


		private void OnShowTargets (object sender, EventArgs args)
		{
			TargetWindow.ShowWindow(locator);
		}
		
		private void OnQuit (object sender, EventArgs args)
		{
			Logger.Info ("OnQuitAction called - terminating application");

			sendingHandler.Stop();
			service.Stop();
			locator.Stop();

			//Gtk.Main.Quit ();
			program.Quit (); // Should this be called instead?
		}
		
		private void OnTrayIconClick (object o, ButtonPressEventArgs args) // handler for mouse click
		{
			if (args.Event.Button == 1) {
				TargetWindow.ShowWindow(locator);
			} else if (args.Event.Button == 3) {
   				// FIXME: Eventually get all these into UIManagerLayout.xml file
      			Menu popupMenu = new Menu();
      			
      			ImageMenuItem targets = new ImageMenuItem (
						Catalog.GetString ("Giver Recipients ..."));
				targets.Image = new Gtk.Image(Utilities.GetIcon ("giver-24", 24));
      			targets.Activated += OnShowTargets;
      			popupMenu.Add (targets);
      			
      			SeparatorMenuItem separator = new SeparatorMenuItem ();
      			popupMenu.Add (separator);
      			
      			ImageMenuItem preferences = new ImageMenuItem (Gtk.Stock.Preferences, null);
      			preferences.Activated += OnPreferences;
      			popupMenu.Add (preferences);

      			ImageMenuItem about = new ImageMenuItem (Gtk.Stock.About, null);
      			about.Activated += OnAbout;
      			popupMenu.Add (about);

      			separator = new SeparatorMenuItem ();
      			popupMenu.Add (separator);

      			ImageMenuItem quit = new ImageMenuItem ( Gtk.Stock.Quit, null);
      			quit.Activated += OnQuit;
      			popupMenu.Add (quit);
      			
				popupMenu.ShowAll(); // shows everything
      			//popupMenu.Popup(null, null, null, IntPtr.Zero, args.Event.Button, args.Event.Time);
      			popupMenu.Popup(null, null, null, args.Event.Button, args.Event.Time);
   			}
		}		
		
		
		#endregion		


		#region Public Static Methods	
		public static void Main(string[] args)
		{
			try 
			{
				Utilities.SetProcessName ("Giver");
				application = GetApplicationWithArgs(args);
				application.StartMainLoop ();
			} 
			catch (Exception e)
			{
				Giver.Logger.Debug("Exception is: {0}", e);
				Exit (-1);
			}
		}

		public static Application GetApplicationWithArgs(string[] args)
		{
			lock(locker)
			{
				if(application == null)
				{
					lock(locker)
					{
						application = new Application(args);
					}
				}
				return application;
			}
		}

		public static void OnExitSignal (int signal)
		{
			if (ExitingEvent != null) ExitingEvent (null, EventArgs.Empty);
			if (signal >= 0) System.Environment.Exit (0);
		}
		
		public static event EventHandler ExitingEvent = null;
		
		public static void Exit (int exitcode)
		{
			OnExitSignal (-1);
			System.Environment.Exit (exitcode);
		}

        // <summary>
        // Connects a Notification to the application icon in the notification area and shows it.
        // </summary>
        public static void ShowAppNotification(Notification notification)
        {
            notification.AttachToWidget(Giver.Application.Instance.trayIcon);
            notification.Show();
        }


		public static void EnqueueFileSend(ServiceInfo serviceInfo, string[] files)
		{
			Giver.Application.Instance.sendingHandler.QueueFileSend(serviceInfo, files);
		}

		#endregion
		
		#region Public Methods			
		public void StartMainLoop ()
		{
			program.Run ();
		}

		public void QuitMainLoop ()
		{
//			actionManager ["QuitAction"].Activate ();
		}
		#endregion
		
	}
}