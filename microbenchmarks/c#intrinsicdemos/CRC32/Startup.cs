#define UseOptions // or NoOptions
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.Intrinsics.X86;
using System.Diagnostics;

namespace web2
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(LogLevel.Debug);
            loggerFactory.AddDebug(LogLevel.Debug);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

#if NoOptions
            #region UseWebSockets
            app.UseWebSockets();
            #endregion
#endif
#if UseOptions
            #region UseWebSocketsOptions
            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            };
            app.UseWebSockets(webSocketOptions);
            #endregion
#endif
            #region AcceptWebSocket
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/ws")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        await CRC(context, webSocket);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    await next();
                }

            });
#endregion
            app.UseFileServer();
        }
#region CRC
			
        private async Task CRC(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[4096 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
				var requestSize = System.Text.Encoding.Default.GetString(buffer, 0, result.Count);

				int num = 0;

				if (requestSize != null) {
					num = Convert.ToInt32(requestSize);
				}
				
				if (num == 0)
				{
					var str = "{\"b\":\"0\",\"s\":0,\"b32\":0,\"b64\":0}";
					var b = Encoding.ASCII.GetBytes(str);
					await webSocket.SendAsync(new ArraySegment<byte>(b, 0, b.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);
				} else {
					string testString = CRC32.RandomString(num);
					double timeSW = CRC32.benchSoftwareHash(testString);
					double time32 = CRC32.benchCRC32HashP(testString);
					double time64 = CRC32.benchCRC32Hash64TP(testString);
					
					System.Console.WriteLine("num = " + num + "timeSW = " + timeSW + "time32 = " + time32 + "time64 = " + time64);
					
					var str = "{\"b\":\"" + num + "\",\"s\":" + timeSW + ",\"b32\":" + time32 + ",\"b64\":" + time64 + "}";
					System.Console.WriteLine(str);
					var b = Encoding.ASCII.GetBytes(str);
					await webSocket.SendAsync(new ArraySegment<byte>(b, 0, b.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);
				}
				result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
				
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
#endregion
    }

	public class CRC32 {
		private static long iteration = 2500000;
		private static Random random = new Random();
		public static string RandomString(int length)
		{
			const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			var x = new string(Enumerable.Repeat(chars, length)
			  .Select(s => s[random.Next(s.Length)]).ToArray());

			return x;
		}

		public static int getHashCode(string str)
		{
            unsafe
            {
                fixed (char* src = str)
                {
                    char* cptr = src;
                    int c;
                    int hashValue1 = 5381;
                    int length = str.Length;
                    for (int i = 0; i < length; i++)
                    {
                        c = cptr[0];
                        hashValue1 = ((hashValue1 << 5) + hashValue1) ^ c;
                        cptr++;
                    }
                    return hashValue1 * 1566083941;
                }
            }
		}

		public static int getHashCodeCRC(string str)
		{
			unsafe
			{
				fixed (char* src = str)
				{
					uint* ptr = (uint*)src;
					int length = str.Length;
					uint hash = 5381;
					int i;
					for (i = 0; i < length; i += 2)
					{
						hash = Sse42.Crc32(hash, ptr[i]);
					}
					if (i > length) // The last two-byte char
					{
						hash = Sse42.Crc32(hash, ((ushort*)ptr)[length - 1]);
					}
					return (int)hash;

				}
			}
		}

		public static int getHashCodeCRCP(string str)
		{
            unsafe
            {
                fixed (char* src = str)
                {
                    uint* ptr = (uint*)src;
                    int length = str.Length;
                    uint hash0 = 5381;
                    uint hash1 = 5381;
                    uint hash2 = 5381;
                    int i = 0;
                    if (length == 1)
                    {
                        return (int)Sse42.Crc32(hash1, ((ushort*)ptr)[0]);
                    }
                    hash2 = Sse42.Crc32(hash2, ptr[0]);
                    int uintLength = length / 2;
                    bool oneMore = length % 2 == 1;
                    for (i = 1; i + 3 <= uintLength; i += 3)
                    {
                        hash0 = Sse42.Crc32(hash0, ptr[i]);
                        hash1 = Sse42.Crc32(hash1, ptr[i + 1]);
                        hash2 = Sse42.Crc32(hash2, ptr[i + 2]);
                    }
                    if (i - uintLength == 2)
                    {
                        hash0 = Sse42.Crc32(hash0, ptr[uintLength - 2]);
                        hash1 = Sse42.Crc32(hash1, ptr[uintLength - 1]);
                    }
                    if (i - uintLength == 1)
                    {
                        hash1 = Sse42.Crc32(hash1, ptr[uintLength - 1]);
                    }
                    if (oneMore)
                    {
                        hash2 = Sse42.Crc32(hash2, ((ushort*)ptr)[length - 1]);
                    }

                    return (int)(Sse42.Crc32(hash2, Sse42.Crc32(hash0, hash1)));

                }
            }
		}

		public static int getHashCodeCRC64TP(string str)
		{
			unsafe
			{
				fixed (char* src = str)
				{
					ulong* ptr = (ulong*)src;
					int length = str.Length;
					ulong hash0 = 5381;
					ulong hash1 = 5381;
					ulong hash2 = 5381;
					int i = 0;

					if (length == 1)
					{
						return (int)Sse42.Crc32(hash1, ((ushort*)ptr)[0]);
					}
					else if (length == 2)
					{
						return (int)Sse42.Crc32(hash1, ((uint*)ptr)[0]);
					}
					else if (length == 3)
					{
						return (int)Sse42.Crc32(Sse42.Crc32(hash1, ((uint*)ptr)[0]), ((ushort*)ptr)[2]);
					}

					hash2 = Sse42.Crc32(hash2, ptr[0]);
					int ulongLength = length / 4;

					for (i = 1; i + 3 <= ulongLength; i += 3)
					{
						hash0 = Sse42.Crc32(hash0, ptr[i]);
						hash1 = Sse42.Crc32(hash1, ptr[i + 1]);
						hash2 = Sse42.Crc32(hash2, ptr[i + 2]);
					}
					bool oneMore = length % 4 == 1;
					bool twoMore = length % 4 == 2;
					bool threeMore = length % 4 == 3;

					if (i - length == 2)
					{
						hash0 = Sse42.Crc32(hash0, ptr[ulongLength - 2]);
						hash1 = Sse42.Crc32(hash1, ptr[ulongLength - 1]);
					}
					else if (i - length == 1)
					{
						hash1 = Sse42.Crc32(hash1, ptr[ulongLength - 1]);
					}

					if (threeMore)
					{
						hash1 = Sse42.Crc32(hash1, ((uint*)ptr)[length>>2 - 1]);
						hash2 = Sse42.Crc32(hash2, ((ushort*)ptr)[length - 1]);
					}
					else if (twoMore)
					{
						hash1 = Sse42.Crc32(hash1, ((uint*)ptr)[length>>2 - 1]);
					}
					else if (oneMore)
					{
						hash2 = Sse42.Crc32(hash2, ((ushort*)ptr)[length - 1]);
					}

					return (int)(Sse42.Crc32(hash2, Sse42.Crc32(hash0, hash1)));

				}
			}
		}

		public static double benchSoftwareHash(string str)
		{
			int hash;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			for (long i = 0; i < iteration; i++)
			{
				hash = getHashCode(str);
			}
			stopWatch.Stop();
			TimeSpan ts = stopWatch.Elapsed;
			double elapsedTime = ts.Minutes * 60.0 + ts.Seconds + ts.Milliseconds / 1000.0;
			return elapsedTime;
		}

		public static double benchCRC32Hash(string str)
		{
			if (!Sse42.IsSupported)
			{
				return 0.0;
			}

			int hash;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			for (long i = 0; i < iteration; i++)
			{
				hash = getHashCodeCRC(str);
			}
			stopWatch.Stop();
			TimeSpan ts = stopWatch.Elapsed;
			double elapsedTime = ts.Minutes * 60.0 + ts.Seconds + ts.Milliseconds / 1000.0;
			return elapsedTime;
		}

		public static double benchCRC32HashP(string str)
		{
			if (!Sse42.IsSupported)
			{
				return 0.0;			}

			int hash;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			for (long i = 0; i < iteration; i++)
			{
				hash = getHashCodeCRCP(str);
			}
			stopWatch.Stop();
			TimeSpan ts = stopWatch.Elapsed;
			double elapsedTime = ts.Minutes * 60.0 + ts.Seconds + ts.Milliseconds / 1000.0;
			return elapsedTime;
		}

		public static double benchCRC32Hash64TP(string str)
		{
			if (!Sse42.IsSupported)
			{
				return 0.0;			}

			int hash;
			Stopwatch stopWatch = new Stopwatch();
			stopWatch.Start();
			for (long i = 0; i < iteration; i++)
			{
				hash = getHashCodeCRC64TP(str);
			}
			stopWatch.Stop();
			TimeSpan ts = stopWatch.Elapsed;
			double elapsedTime = ts.Minutes * 60.0 + ts.Seconds + ts.Milliseconds / 1000.0;
			return elapsedTime;
		}
	}

}