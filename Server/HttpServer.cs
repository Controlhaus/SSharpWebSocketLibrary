#region License
/*
 * HttpServer.cs
 *
 * A simple HTTP server that allows to accept the WebSocket connection requests.
 *
 * The MIT License
 *
 * Copyright (c) 2012-2016 sta.blockhead
 * Copyright � 2016 Nivloc Enterprises Ltd
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
#endregion

#region Contributors
/*
 * Contributors:
 * - Juan Manuel Lallana <juan.manuel.lallana@gmail.com>
 * - Liryna <liryna.stark@gmail.com>
 * - Rohan Singh <rohan-singh@hotmail.com>
 */
#endregion

using System;
using System.Collections.Generic;
#if SSHARP
using Crestron.SimplSharp.CrestronIO;
using SSMono.Threading;
using Environment = SSMono.Environment;
using File = SSMono.IO.File;
using Activator = Crestron.SimplSharp.Reflection.ActivatorEx;
using SSMono.Security.Principal;
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharp;
using SSMono.Net;
using HttpUtility = SSMono.Web.HttpUtility;
using NetworkCredential = WebSocketSharp.Net.NetworkCredential;
#else
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Security.Principal;
#endif
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
	{
	/// <summary>
	/// Provides a simple HTTP server that allows to accept the WebSocket connection requests.
	/// </summary>
	/// <remarks>
	/// The HttpServer class can provide multiple WebSocket services.
	/// </remarks>
	public class HttpServer
		{
		#region Private Fields

		private IPAddress _address;
		private string _hostname;
		private HttpListener _listener;
		private Logger _logger;
		private int _port;
		private Thread _receiveThread;
		private bool _secure;
		private WebSocketServiceManager _services;
		private volatile ServerState _state;
		private object _sync;
		private bool _windows;
#if SSHARP
		private EthernetAdapterType _adapter;
#endif

		#endregion

		#region Public Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpServer"/> class.
		/// </summary>
		/// <remarks>
		/// An instance initialized by this constructor listens for the incoming requests on port 80.
		/// </remarks>
		public HttpServer ()
			{
			init ("*", IPAddress.Any, 80, false
#if SSHARP
, EthernetAdapterType.EthernetUnknownAdapter
#endif
);
			}

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpServer"/> class with the specified
		/// <paramref name="port"/>.
		/// </summary>
		/// <remarks>
		///   <para>
		///   An instance initialized by this constructor listens for the incoming requests
		///   on <paramref name="port"/>.
		///   </para>
		///   <para>
		///   If <paramref name="port"/> is 443, that instance provides a secure connection.
		///   </para>
		/// </remarks>
		/// <param name="port">
		/// An <see cref="int"/> that represents the port number on which to listen.
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="port"/> isn't between 1 and 65535 inclusive.
		/// </exception>
		public HttpServer (int port)
			: this (port, port == 443)
			{
			}

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpServer"/> class with
		/// the specified HTTP URL.
		/// </summary>
		/// <remarks>
		///   <para>
		///   An instance initialized by this constructor listens for the incoming requests on
		///   the host name and port in <paramref name="url"/>.
		///   </para>
		///   <para>
		///   If <paramref name="url"/> doesn't include a port, either port 80 or 443 is used on
		///   which to listen. It's determined by the scheme (http or https) in <paramref name="url"/>.
		///   (Port 80 if the scheme is http.)
		///   </para>
		/// </remarks>
		/// <param name="url">
		/// A <see cref="string"/> that represents the HTTP URL of the server.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="url"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		///   <para>
		///   <paramref name="url"/> is empty.
		///   </para>
		///   <para>
		///   -or-
		///   </para>
		///   <para>
		///   <paramref name="url"/> is invalid.
		///   </para>
		/// </exception>
		public HttpServer (string url)
#if SSHARP
			: this (url, EthernetAdapterType.EthernetUnknownAdapter)
			{
			}

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpServer"/> class with
		/// the specified HTTP URL.
		/// </summary>
		/// <remarks>
		///   <para>
		///   An instance initialized by this constructor listens for the incoming requests on
		///   the host name and port in <paramref name="url"/>.
		///   </para>
		///   <para>
		///   If <paramref name="url"/> doesn't include a port, either port 80 or 443 is used on
		///   which to listen. It's determined by the scheme (http or https) in <paramref name="url"/>.
		///   (Port 80 if the scheme is http.)
		///   </para>
		/// </remarks>
		/// <param name="url">
		/// A <see cref="string"/> that represents the HTTP URL of the server.
		/// </param>
		/// <param name="adapter">
		/// An EthernetAdapterType for the EthernetAdapter to use for the connection
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="url"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		///   <para>
		///   <paramref name="url"/> is empty.
		///   </para>
		///   <para>
		///   -or-
		///   </para>
		///   <para>
		///   <paramref name="url"/> is invalid.
		///   </para>
		/// </exception>
		public HttpServer (string url, EthernetAdapterType adapter)
			{
#endif
			if (url == null)
				throw new ArgumentNullException ("url");

			if (url.Length == 0)
				throw new ArgumentException ("An empty string.", "url");

			Uri uri;
			string msg;
			if (!tryCreateUri (url, out uri, out msg))
				throw new ArgumentException (msg, "url");

			var host = getHost (uri);
			var addr = host.ToIPAddress ();
			if (!addr.IsLocal ())
				throw new ArgumentException ("The host part isn't a local host name: " + url, "url");

			init (host, addr, uri.Port, uri.Scheme == "https"
#if SSHARP
, adapter
#endif
);

			}

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpServer"/> class with the specified
		/// <paramref name="port"/> and <paramref name="secure"/>.
		/// </summary>
		/// <remarks>
		/// An instance initialized by this constructor listens for the incoming requests
		/// on <paramref name="port"/>.
		/// </remarks>
		/// <param name="port">
		/// An <see cref="int"/> that represents the port number on which to listen.
		/// </param>
		/// <param name="secure">
		/// A <see cref="bool"/> that indicates providing a secure connection or not.
		/// (<c>true</c> indicates providing a secure connection.)
		/// </param>
		/// <exception cref="ArgumentException">
		/// Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="port"/> isn't between 1 and 65535 inclusive.
		/// </exception>
		public HttpServer (int port, bool secure)
#if SSHARP
			: this (port, secure, EthernetAdapterType.EthernetUnknownAdapter)
			{
			}

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpServer"/> class with the specified
		/// <paramref name="port"/> and <paramref name="secure"/>.
		/// </summary>
		/// <remarks>
		/// An instance initialized by this constructor listens for the incoming requests
		/// on <paramref name="port"/>.
		/// </remarks>
		/// <param name="port">
		/// An <see cref="int"/> that represents the port number on which to listen.
		/// </param>
		/// <param name="secure">
		/// A <see cref="bool"/> that indicates providing a secure connection or not.
		/// (<c>true</c> indicates providing a secure connection.)
		/// </param>
		/// <param name="adapter">
		/// An EthernetAdapterType for the EthernetAdapter to use for the connection
		/// </param>
		/// <exception cref="ArgumentException">
		/// Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="port"/> isn't between 1 and 65535 inclusive.
		/// </exception>
		public HttpServer (int port, bool secure, EthernetAdapterType adapter)
			{
#endif
			if (!port.IsPortNumber ())
				throw new ArgumentOutOfRangeException ("port", "Not between 1 and 65535 inclusive: " + port);

			init ("*", IPAddress.Any, port, secure
#if SSHARP
, adapter
#endif
);

			Log.Info ("HttpServer created on port {0} on adapter {1} with security {2}", port, adapter, secure);
			}

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpServer"/> class with
		/// the specified <paramref name="address"/> and <paramref name="port"/>.
		/// </summary>
		/// <remarks>
		///   <para>
		///   An instance initialized by this constructor listens for the incoming requests on
		///   <paramref name="address"/> and <paramref name="port"/>.
		///   </para>
		///   <para>
		///   If <paramref name="port"/> is 443, that instance provides a secure connection.
		///   </para>
		/// </remarks>
		/// <param name="address">
		/// A <see cref="IPAddress"/> that represents the local IP address of the server.
		/// </param>
		/// <param name="port">
		/// An <see cref="int"/> that represents the port number on which to listen.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="address"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="address"/> isn't a local IP address.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="port"/> isn't between 1 and 65535 inclusive.
		/// </exception>
		public HttpServer (IPAddress address, int port)
			: this (address, port, port == 443)
			{
			}

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpServer"/> class with
		/// the specified <paramref name="address"/>, <paramref name="port"/>,
		/// and <paramref name="secure"/>.
		/// </summary>
		/// <remarks>
		///   An instance initialized by this constructor listens for the incoming requests on
		///   <paramref name="address"/> and <paramref name="port"/>.
		/// </remarks>
		/// <param name="address">
		/// A <see cref="IPAddress"/> that represents the local IP address of the server.
		/// </param>
		/// <param name="port">
		/// An <see cref="int"/> that represents the port number on which to listen.
		/// </param>
		/// <param name="secure">
		/// A <see cref="bool"/> that indicates providing a secure connection or not.
		/// (<c>true</c> indicates providing a secure connection.)
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="address"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="address"/> isn't a local IP address.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="port"/> isn't between 1 and 65535 inclusive.
		/// </exception>
		public HttpServer (IPAddress address, int port, bool secure)
#if SSHARP
			: this (address, port, secure, EthernetAdapterType.EthernetUnknownAdapter)
			{
			}

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpServer"/> class with
		/// the specified <paramref name="address"/>, <paramref name="port"/>,
		/// and <paramref name="secure"/>.
		/// </summary>
		/// <remarks>
		///   An instance initialized by this constructor listens for the incoming requests on
		///   <paramref name="address"/> and <paramref name="port"/>.
		/// </remarks>
		/// <param name="address">
		/// A <see cref="IPAddress"/> that represents the local IP address of the server.
		/// </param>
		/// <param name="port">
		/// An <see cref="int"/> that represents the port number on which to listen.
		/// </param>
		/// <param name="secure">
		/// A <see cref="bool"/> that indicates providing a secure connection or not.
		/// (<c>true</c> indicates providing a secure connection.)
		/// </param>
		/// <param name="adapter">
		/// An EthernetAdapterType for the EthernetAdapter to use for the connection
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="address"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="address"/> isn't a local IP address.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="port"/> isn't between 1 and 65535 inclusive.
		/// </exception>
		public HttpServer (IPAddress address, int port, bool secure, EthernetAdapterType adapter)
			{
			if (address == null)
				throw new ArgumentNullException ("address");

			if (!address.IsLocal ())
				throw new ArgumentException ("Not a local IP address: " + address, "address");

			if (!port.IsPortNumber ())
				throw new ArgumentOutOfRangeException (
				  "port", "Not between 1 and 65535 inclusive: " + port);

			init (null, address, port, secure, adapter);
			}
#endif

		#endregion

		#region Public Properties

#if SSHARP
		/// <summary>
		/// Gets the ethernet adapter this server is using to listen for incoming requests
		/// </summary>
		public EthernetAdapterType Adapter
			{
			get { return _adapter; }
			}
#endif

		/// <summary>
		/// Gets the local IP address of the server.
		/// </summary>
		/// <value>
		/// A <see cref="IPAddress"/> that represents the local IP address of the server.
		/// </value>
		public IPAddress Address
			{
			get
				{
				return _address;
				}
			}

		/// <summary>
		/// Gets or sets the scheme used to authenticate the clients.
		/// </summary>
		/// <value>
		/// One of the <see cref="WebSocketSharp.Net.AuthenticationSchemes"/> enum values,
		/// indicates the scheme used to authenticate the clients. The default value is
		/// <see cref="WebSocketSharp.Net.AuthenticationSchemes.Anonymous"/>.
		/// </value>
		public AuthenticationSchemes AuthenticationSchemes
			{
			get
				{
				return _listener.AuthenticationSchemes;
				}

			set
				{
				var msg = _state.CheckIfAvailable (true, false, false);
				if (msg != null)
					{
					_logger.Error (msg);
					return;
					}

				_listener.AuthenticationSchemes = value;
				}
			}

		/// <summary>
		/// Gets a value indicating whether the server has started.
		/// </summary>
		/// <value>
		/// <c>true</c> if the server has started; otherwise, <c>false</c>.
		/// </value>
		public bool IsListening
			{
			get
				{
				return _state == ServerState.Start;
				}
			}

		/// <summary>
		/// Gets a value indicating whether the server provides a secure connection.
		/// </summary>
		/// <value>
		/// <c>true</c> if the server provides a secure connection; otherwise, <c>false</c>.
		/// </value>
		public bool IsSecure
			{
			get
				{
				return _secure;
				}
			}

		/// <summary>
		/// Gets or sets a value indicating whether the server cleans up the inactive sessions
		/// in the WebSocket services periodically.
		/// </summary>
		/// <value>
		/// <c>true</c> if the server cleans up the inactive sessions every 60 seconds;
		/// otherwise, <c>false</c>. The default value is <c>true</c>.
		/// </value>
		public bool KeepClean
			{
			get
				{
				return _services.KeepClean;
				}

			set
				{
				var msg = _state.CheckIfAvailable (true, false, false);
				if (msg != null)
					{
					_logger.Error (msg);
					return;
					}

				_services.KeepClean = value;
				}
			}

		/// <summary>
		/// Gets the logging functions.
		/// </summary>
		/// <remarks>
		/// The default logging level is <see cref="LogLevel.Error"/>. If you would like to change it,
		/// you should set the <c>Log.Level</c> property to any of the <see cref="LogLevel"/> enum
		/// values.
		/// </remarks>
		/// <value>
		/// A <see cref="Logger"/> that provides the logging functions.
		/// </value>
		public Logger Log
			{
			get
				{
				return _logger;
				}
			}

		/// <summary>
		/// Gets the port on which to listen for incoming requests.
		/// </summary>
		/// <value>
		/// An <see cref="int"/> that represents the port number on which to listen.
		/// </value>
		public int Port
			{
			get
				{
				return _port;
				}
			}

		/// <summary>
		/// Gets or sets the name of the realm associated with the server.
		/// </summary>
		/// <remarks>
		/// If this property is <see langword="null"/> or empty, <c>"SECRET AREA"</c> will be used as
		/// the name of the realm.
		/// </remarks>
		/// <value>
		/// A <see cref="string"/> that represents the name of the realm. The default value is
		/// <see langword="null"/>.
		/// </value>
		public string Realm
			{
			get
				{
				return _listener.Realm;
				}

			set
				{
				var msg = _state.CheckIfAvailable (true, false, false);
				if (msg != null)
					{
					_logger.Error (msg);
					return;
					}

				_listener.Realm = value;
				}
			}

		/// <summary>
		/// Gets or sets a value indicating whether the server is allowed to be bound to an address
		/// that is already in use.
		/// </summary>
		/// <remarks>
		/// If you would like to resolve to wait for socket in <c>TIME_WAIT</c> state, you should set
		/// this property to <c>true</c>.
		/// </remarks>
		/// <value>
		/// <c>true</c> if the server is allowed to be bound to an address that is already in use;
		/// otherwise, <c>false</c>. The default value is <c>false</c>.
		/// </value>
		public bool ReuseAddress
			{
			get
				{
				return _listener.ReuseAddress;
				}

			set
				{
				var msg = _state.CheckIfAvailable (true, false, false);
				if (msg != null)
					{
					_logger.Error (msg);
					return;
					}

				_listener.ReuseAddress = value;
				}
			}

		/// <summary>
		/// Gets or sets the document root path of the server.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that represents the document root path of the server.
		/// The default value is <c>"./Public"</c>.
		/// </value>
		public string RootPath
			{
			get
				{
				return _listener.RootPath;
				}

			set
				{
				var msg = _state.CheckIfAvailable (true, false, false);
				if (msg != null)
					{
					_logger.Error (msg);
					return;
					}

				_listener.RootPath = value;
				}
			}

#if !NETCF || BCC || SSL
		/// <summary>
		/// Gets or sets the SSL configuration used to authenticate the server and
		/// optionally the client for secure connection.
		/// </summary>
		/// <value>
		/// A <see cref="ServerSslConfiguration"/> that represents the configuration used
		/// to authenticate the server and optionally the client for secure connection.
		/// </value>
		public ServerSslConfiguration SslConfiguration
			{
			get
				{
				return _listener.SslConfiguration;
				}

			set
				{
				var msg = _state.CheckIfAvailable (true, false, false);
				if (msg != null)
					{
					_logger.Error (msg);
					return;
					}

				_listener.SslConfiguration = value;
				}
			}
#endif

		/// <summary>
		/// Gets or sets the delegate called to find the credentials for an identity used to
		/// authenticate a client.
		/// </summary>
		/// <value>
		/// A <c>Func&lt;<see cref="IIdentity"/>, <see cref="NetworkCredential"/>&gt;</c> delegate
		/// that references the method(s) used to find the credentials. The default value is
		/// <see langword="null"/>.
		/// </value>
		public Func<IIdentity, NetworkCredential> UserCredentialsFinder
			{
			get
				{
				return _listener.UserCredentialsFinder;
				}

			set
				{
				var msg = _state.CheckIfAvailable (true, false, false);
				if (msg != null)
					{
					_logger.Error (msg);
					return;
					}

				_listener.UserCredentialsFinder = value;
				}
			}

		/// <summary>
		/// Gets or sets a user defined state object for this HttpServer instance
		/// </summary>
		public object UserState
			{
			get { return _listener.UserState; }
			set { _listener.UserState = value; }
			}

		/// <summary>
		/// Gets or sets the wait time for the response to the WebSocket Ping or Close.
		/// </summary>
		/// <value>
		/// A <see cref="TimeSpan"/> that represents the wait time. The default value is
		/// the same as 1 second.
		/// </value>
		public TimeSpan WaitTime
			{
			get
				{
				return _services.WaitTime;
				}

			set
				{
				var msg = _state.CheckIfAvailable (true, false, false) ?? value.CheckIfValidWaitTime ();
				if (msg != null)
					{
					_logger.Error (msg);
					return;
					}

				_services.WaitTime = value;
				}
			}

		/// <summary>
		/// Gets the access to the WebSocket services provided by the server.
		/// </summary>
		/// <value>
		/// A <see cref="WebSocketServiceManager"/> that manages the WebSocket services.
		/// </value>
		public WebSocketServiceManager WebSocketServices
			{
			get
				{
				return _services;
				}
			}

		#endregion

		#region Public Events

		/// <summary>
		/// Occurs when the server receives any request.
		/// </summary>
		public event EventHandler<HttpRequestCancelEventArgs> OnRequest;

		/// <summary>
		/// Occurs when the server receives any request.
		/// </summary>
		public event EventHandler<HttpResolveWebSocketServiceHostEventArgs> OnResolveWebSocketServiceHost;

		/// <summary>
		/// Occurs when the server receives an HTTP CONNECT request.
		/// </summary>
		public event EventHandler<HttpRequestEventArgs> OnConnect;

		/// <summary>
		/// Occurs when the server receives an HTTP DELETE request.
		/// </summary>
		public event EventHandler<HttpRequestEventArgs> OnDelete;

		/// <summary>
		/// Occurs when the server receives an HTTP GET request.
		/// </summary>
		public event EventHandler<HttpRequestEventArgs> OnGet;

		/// <summary>
		/// Occurs when the server receives an HTTP HEAD request.
		/// </summary>
		public event EventHandler<HttpRequestEventArgs> OnHead;

		/// <summary>
		/// Occurs when the server receives an HTTP OPTIONS request.
		/// </summary>
		public event EventHandler<HttpRequestEventArgs> OnOptions;

		/// <summary>
		/// Occurs when the server receives an HTTP PATCH request.
		/// </summary>
		public event EventHandler<HttpRequestEventArgs> OnPatch;

		/// <summary>
		/// Occurs when the server receives an HTTP POST request.
		/// </summary>
		public event EventHandler<HttpRequestEventArgs> OnPost;

		/// <summary>
		/// Occurs when the server receives an HTTP PUT request.
		/// </summary>
		public event EventHandler<HttpRequestEventArgs> OnPut;

		/// <summary>
		/// Occurs when the server receives an HTTP TRACE request.
		/// </summary>
		public event EventHandler<HttpRequestEventArgs> OnTrace;

		#endregion

		#region Private Methods

		private void abort ()
			{
			lock (_sync)
				{
				if (!IsListening)
					return;

				_state = ServerState.ShuttingDown;
				}

			_services.Stop (new CloseEventArgs (CloseStatusCode.ServerError), true, false);
			_listener.Abort ();

			_state = ServerState.Stop;
			}

		private bool checkIfAvailable (bool ready, bool start, bool shutting, bool stop, out string message)
			{
			message = null;

			if (!ready && _state == ServerState.Ready)
				{
				message = "This operation is not available in: ready";
				return false;
				}

			if (!start && _state == ServerState.Start)
				{
				message = "This operation is not available in: start";
				return false;
				}

			if (!shutting && _state == ServerState.ShuttingDown)
				{
				message = "This operation is not available in: shutting down";
				return false;
				}

			if (!stop && _state == ServerState.Stop)
				{
				message = "This operation is not available in: stop";
				return false;
				}

			return true;
			}

#if !NETCF || BCC || SSL
		private string checkIfCertificateExists ()
			{
			if (!_secure)
				return null;

			var usr = _listener.SslConfiguration.ServerCertificate != null;
			var port = EndPointListener.CertificateExists (_port, _listener.CertificateFolderPath);
			if (usr && port)
				{
				_logger.Warn ("The server certificate associated with the port number already exists.");
				return null;
				}

			return !(usr || port)
					? "The secure connection requires a server certificate."
					: null;
			}
#endif

		private static string convertToString (IPAddress address)
			{
			return address.AddressFamily == AddressFamily.InterNetworkV6
				 ? String.Format ("[{0}]", address.ToString ())
				 : address.ToString ();
			}

		private static string getHost (Uri uri)
			{
			return uri.HostNameType == UriHostNameType.IPv6 ? uri.Host : uri.DnsSafeHost;
			}

		private void init (string hostname, IPAddress address, int port, bool secure
#if SSHARP
, EthernetAdapterType adapter
#endif
)
			{
			_hostname = hostname ?? convertToString (address);
			_address = address;
			_port = port;
			_secure = secure;
#if SSHARP
			_adapter = adapter;
#endif

			_listener = new HttpListener ();
			_listener.Prefixes.Add (
			  String.Format ("http{0}://{1}:{2}/", secure ? "s" : "", _hostname, port));

			_logger = _listener.Log;
			_services = new WebSocketServiceManager (_logger);
			_sync = new object ();

			var os = Environment.OSVersion;
#if NETCF
			_windows = os.Platform != PlatformID.Unix;
#else
			_windows = os.Platform != PlatformID.Unix && os.Platform != PlatformID.MacOSX;
#endif
			}

		/*
		private static readonly Dictionary<string, FieldInfo> dictMethods = new Dictionary<string, FieldInfo>
			{
			{"GET", ((CType)typeof (HttpServer)).GetField ("OnGet", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)},
			{"HEAD", ((CType)typeof (HttpServer)).GetField ("OnHead", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)},
			{"POST", ((CType)typeof (HttpServer)).GetField ("OnPost", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)},
			{"PUT", ((CType)typeof (HttpServer)).GetField ("OnPut", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)},
			{"DELETE", ((CType)typeof (HttpServer)).GetField ("OnDelete", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)},
			{"OPTIONS", ((CType)typeof (HttpServer)).GetField ("OnOptions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)},
			{"TRACE", ((CType)typeof (HttpServer)).GetField ("OnTrace", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)},
			{"CONNECT", ((CType)typeof (HttpServer)).GetField ("OnConnect", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)},
			{"PATCH", ((CType)typeof (HttpServer)).GetField ("OnPatch", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)},
			};

		private void processRequest (HttpListenerContext context)
			{
			FieldInfo fi;
			if (!dictMethods.TryGetValue (context.Request.HttpMethod, out fi))
				context.Response.StatusCode = (int)HttpStatusCode.NotImplemented;
			else
				{
				var evt = (EventHandler<HttpRequestEventArgs>)fi.GetValue (this);
				if (evt != null)
					evt (this, new HttpRequestEventArgs (context));
				}

			var method = context.Request.HttpMethod;
			var evtx = method == "GET"
					  ? OnGet
					  : method == "HEAD"
						? OnHead
						: method == "POST"
						  ? OnPost
						  : method == "PUT"
							? OnPut
							: method == "DELETE"
							  ? OnDelete
							  : method == "OPTIONS"
								? OnOptions
								: method == "TRACE"
								  ? OnTrace
								  : method == "CONNECT"
									? OnConnect
									: method == "PATCH"
									  ? OnPatch
									  : null;

			if (evt != null)
				evt (this, new HttpRequestEventArgs (context));
			else
				context.Response.StatusCode = (int)HttpStatusCode.NotImplemented;
			*/

		private void processRequest (HttpListenerContext context)
			{
			if (OnRequest != null)
				{
				var cea = new HttpRequestCancelEventArgs (context);
				OnRequest (this, cea);
				if (!cea.Cancel)
					context.Response.Close ();
				return;
				}

			EventHandler<HttpRequestEventArgs> evt;
			var method = context.Request.HttpMethod;
			switch (method)
				{
				case ("GET"):
					evt = OnGet;
					break;
				case ("HEAD"):
					evt = OnHead;
					break;
				case ("POST"):
					evt = OnPost;
					break;
				case ("PUT"):
					evt = OnPut;
					break;
				case ("DELETE"):
					evt = OnDelete;
					break;
				case ("OPTIONS"):
					evt = OnOptions;
					break;
				case ("TRACE"):
					evt = OnTrace;
					break;
				case ("CONNECT"):
					evt = OnConnect;
					break;
				case ("PATCH"):
					evt = OnPatch;
					break;
				default:
					evt = null;
					break;
				}

			if (evt != null)
				evt (this, new HttpRequestEventArgs (context));
			else
				context.Response.StatusCode = (int)HttpStatusCode.NotImplemented;

			context.Response.Close ();
			}

		private void processRequest (HttpListenerWebSocketContext context)
			{
			WebSocketServiceHost host;
			string path = context.RequestUri.AbsolutePath;
			if (!_services.InternalTryGetServiceHost (path, out host, false))
				{
				if (OnResolveWebSocketServiceHost != null)
					{
					var hrwsshea = new HttpResolveWebSocketServiceHostEventArgs (HttpUtility.UrlDecode (path).TrimEndSlash ());
					OnResolveWebSocketServiceHost (this, hrwsshea);
					host = hrwsshea.Host;
					}

				if (host == null)
					{
					context.Close (HttpStatusCode.NotImplemented);
					return;
					}
				}

			host.StartSession (context);
			}

		private void receiveRequest ()
			{
			while (true)
				{
				try
					{
					var ctx = _listener.GetContext ();
					HttpListenerContext.Current = ctx;

					ThreadPool.QueueUserWorkItem (
					  state =>
						  {
						  try
							  {
							  if (ctx.Request.IsUpgradeTo ("websocket"))
								  {
								  processRequest (ctx.AcceptWebSocket (null));
								  return;
								  }

							  processRequest (ctx);
							  }
						  catch (Exception ex)
							  {
							  _logger.Fatal (ex.ToString ());
							  ctx.Connection.Close (true);
							  }
						  });
					}
				catch (HttpListenerException ex)
					{
					_logger.Warn ("Receiving has been stopped.\nreason: " + ex.Message);
					break;
					}
				catch (Exception ex)
					{
					_logger.Fatal (ex.ToString ());
					break;
					}
				}

			if (IsListening)
				abort ();
			}

		private void startReceiving ()
			{
			_listener.Start ();
			_receiveThread = new Thread (new ThreadStart (receiveRequest));
#if !NETCF
			_receiveThread.IsBackground = true;
#endif
			_receiveThread.Start ();
			}

		private void stopReceiving (int millisecondsTimeout)
			{
			_listener.Close ();
			_receiveThread.Join (millisecondsTimeout);
			}

		private static bool tryCreateUri (string uriString, out Uri result, out string message)
			{
			result = null;

			var uri = uriString.ToUri ();
			if (uri == null)
				{
				message = "An invalid URI string: " + uriString;
				return false;
				}

			if (!uri.IsAbsoluteUri)
				{
				message = "Not an absolute URI: " + uriString;
				return false;
				}

			var schm = uri.Scheme;
			if (!(schm == "http" || schm == "https"))
				{
				message = "The scheme part isn't 'http' or 'https': " + uriString;
				return false;
				}

			if (uri.PathAndQuery != "/")
				{
				message = "Includes the path or query component: " + uriString;
				return false;
				}

			if (uri.Fragment.Length > 0)
				{
				message = "Includes the fragment component: " + uriString;
				return false;
				}

			if (uri.Port == 0)
				{
				message = "The port part is zero: " + uriString;
				return false;
				}

			result = uri;
			message = String.Empty;

			return true;
			}

		#endregion

		#region Public Methods

		/// <summary>
		/// Adds a WebSocket service with the specified behavior and <paramref name="path"/>.
		/// </summary>
		/// <remarks>
		/// This method converts <paramref name="path"/> to URL-decoded string,
		/// and removes <c>'/'</c> from tail end of <paramref name="path"/>.
		/// </remarks>
		/// <param name="path">
		/// A <see cref="string"/> that represents the absolute path to the service to add.
		/// </param>
		/// <typeparam name="TBehaviorWithNew">
		/// The type of the behavior of the service to add. The TBehaviorWithNew must inherit
		/// the <see cref="WebSocketBehavior"/> class, and must have a public parameterless
		/// constructor.
		/// </typeparam>
		public void AddWebSocketService<TBehaviorWithNew> (string path)
		  where TBehaviorWithNew : WebSocketBehavior, new ()
			{
#if SSHARP
			AddWebSocketService<TBehaviorWithNew> (path, Activator.CreateInstance<TBehaviorWithNew>);
#else
			AddWebSocketService<TBehaviorWithNew> (path, () => new TBehaviorWithNew ());
#endif
			}

		/// <summary>
		/// Adds the WebSocket service with the specified behavior, <paramref name="path"/>,
		/// and <paramref name="initializer"/>.
		/// </summary>
		/// <remarks>
		///   <para>
		///   This method converts <paramref name="path"/> to URL-decoded string,
		///   and removes <c>'/'</c> from tail end of <paramref name="path"/>.
		///   </para>
		///   <para>
		///   <paramref name="initializer"/> returns an initialized specified typed
		///   <see cref="WebSocketBehavior"/> instance.
		///   </para>
		/// </remarks>
		/// <param name="path">
		/// A <see cref="string"/> that represents the absolute path to the service to add.
		/// </param>
		/// <param name="initializer">
		/// A Func&lt;T&gt; delegate that references the method used to initialize a new specified
		/// typed <see cref="WebSocketBehavior"/> instance (a new <see cref="IWebSocketSession"/>
		/// instance).
		/// </param>
		/// <typeparam name="TBehavior">
		/// The type of the behavior of the service to add. The TBehavior must inherit
		/// the <see cref="WebSocketBehavior"/> class.
		/// </typeparam>
		public void AddWebSocketService<TBehavior> (string path, Func<TBehavior> initializer)
		  where TBehavior : WebSocketBehavior
			{
			var msg = path.CheckIfValidServicePath () ??
					  (initializer == null ? "'initializer' is null." : null);

			if (msg != null)
				{
				_logger.Error (msg);
				return;
				}

			_services.Add<TBehavior> (path, initializer);
			}

		/// <summary>
		/// Gets the contents of the file with the specified <paramref name="path"/>.
		/// </summary>
		/// <returns>
		/// An array of <see cref="byte"/> that receives the contents of the file,
		/// or <see langword="null"/> if it doesn't exist.
		/// </returns>
		/// <param name="path">
		/// A <see cref="string"/> that represents the virtual path to the file to find.
		/// </param>
		public byte[] GetFile (string path)
			{
			path = Path.Combine (RootPath, path);
			if (_windows)
				path = path.Replace ("/", "\\");

			return File.Exists (path) ? File.ReadAllBytes (path) : null;
			}

		/// <summary>
		/// Removes the WebSocket service with the specified <paramref name="path"/>.
		/// </summary>
		/// <remarks>
		/// This method converts <paramref name="path"/> to URL-decoded string,
		/// and removes <c>'/'</c> from tail end of <paramref name="path"/>.
		/// </remarks>
		/// <returns>
		/// <c>true</c> if the service is successfully found and removed; otherwise, <c>false</c>.
		/// </returns>
		/// <param name="path">
		/// A <see cref="string"/> that represents the absolute path to the service to find.
		/// </param>
		public bool RemoveWebSocketService (string path)
			{
			var msg = path.CheckIfValidServicePath ();
			if (msg != null)
				{
				_logger.Error (msg);
				return false;
				}

			return _services.Remove (path);
			}

		/// <summary>
		/// Starts receiving the HTTP requests.
		/// </summary>
		public void Start ()
			{
			lock (_sync)
				{
				var msg = _state.CheckIfAvailable (true, false, false)
#if !NETCF || BCC || SSL
 ?? checkIfCertificateExists ()
#endif
;
				if (msg != null)
					{
					_logger.Error (msg);
					return;
					}

				_services.Start ();
				startReceiving ();

				_state = ServerState.Start;
				}
			}

		/// <summary>
		/// Stops receiving the incoming requests, and closes the connections.
		/// </summary>
		public void Stop ()
			{
			string msg;
			if (!checkIfAvailable (false, true, false, false, out msg))
				{
				_logger.Error (msg);
				return;
				}

			lock (_sync)
				{
				if (!checkIfAvailable (false, true, false, false, out msg))
					{
					_logger.Error (msg);
					return;
					}

				_state = ServerState.ShuttingDown;
				}

			_services.Stop (new CloseEventArgs (), true, true);
			stopReceiving (5000);

			_state = ServerState.Stop;
			}

		/// <summary>
		/// Stops receiving the incoming requests, and closes the connections with
		/// the specified <paramref name="code"/> and <paramref name="reason"/> for
		/// the WebSocket connection close.
		/// </summary>
		/// <param name="code">
		/// A <see cref="ushort"/> that represents the status code indicating
		/// the reason for the WebSocket connection close. The status codes are
		/// defined in <see href="http://tools.ietf.org/html/rfc6455#section-7.4">
		/// Section 7.4</see> of RFC 6455.
		/// </param>
		/// <param name="reason">
		/// A <see cref="string"/> that represents the reason for the WebSocket
		/// connection close. The size must be 123 bytes or less.
		/// </param>
		public void Stop (ushort code, string reason)
			{
			string msg;
			if (!checkIfAvailable (false, true, false, false, out msg))
				{
				_logger.Error (msg);
				return;
				}

			if (!WebSocket.CheckParametersForClose (code, reason, false, out msg))
				{
				_logger.Error (msg);
				return;
				}

			lock (_sync)
				{
				if (!checkIfAvailable (false, true, false, false, out msg))
					{
					_logger.Error (msg);
					return;
					}

				_state = ServerState.ShuttingDown;
				}

			if (code == (ushort)CloseStatusCode.NoStatus)
				{
				_services.Stop (new CloseEventArgs (), true, true);
				}
			else
				{
				var send = !code.IsReserved ();
				_services.Stop (new CloseEventArgs (code, reason), send, send);
				}

			stopReceiving (5000);

			_state = ServerState.Stop;
			}

		/// <summary>
		/// Stops receiving the incoming requests, and closes the connections with
		/// the specified <paramref name="code"/> and <paramref name="reason"/> for
		/// the WebSocket connection close.
		/// </summary>
		/// <param name="code">
		/// One of the <see cref="CloseStatusCode"/> enum values that represents
		/// the status code indicating the reason for the WebSocket connection close.
		/// </param>
		/// <param name="reason">
		/// A <see cref="string"/> that represents the reason for the WebSocket
		/// connection close. The size must be 123 bytes or less.
		/// </param>
		public void Stop (CloseStatusCode code, string reason)
			{
			string msg;
			if (!checkIfAvailable (false, true, false, false, out msg))
				{
				_logger.Error (msg);
				return;
				}

			if (!WebSocket.CheckParametersForClose (code, reason, false, out msg))
				{
				_logger.Error (msg);
				return;
				}

			lock (_sync)
				{
				if (!checkIfAvailable (false, true, false, false, out msg))
					{
					_logger.Error (msg);
					return;
					}

				_state = ServerState.ShuttingDown;
				}

			if (code == CloseStatusCode.NoStatus)
				{
				_services.Stop (new CloseEventArgs (), true, true);
				}
			else
				{
				var send = !code.IsReserved ();
				_services.Stop (new CloseEventArgs (code, reason), send, send);
				}

			stopReceiving (5000);

			_state = ServerState.Stop;
			}

		#endregion
		}
	}
