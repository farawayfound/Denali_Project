﻿using System;
using System.Threading.Tasks;
using Coinbase.Pro.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocket4Net;


namespace Coinbase.Pro.WebSockets
{
   public class WebSocketConfig
   {
      public string ApiKey { get; set; }
      public string Secret { get; set; }
      public string Passphrase { get; set; }

      public bool UseTimeApi { get; set; } = false;
      public string SocketUri { get; set; } = CoinbaseProWebSocket.Endpoint;

      public void EnsureValid()
      {
      }
   }

   public class CoinbaseProWebSocket : IDisposable
   {
      public const string Endpoint = "wss://ws-feed.pro.coinbase.com";

      public WebSocket RawSocket { get; private set; }

      public CoinbaseProWebSocket(WebSocketConfig config = null)
      {
         this.Config = config ?? new WebSocketConfig();
      }

      public WebSocketConfig Config { get; }

      protected TaskCompletionSource<bool> connecting;

      public Task ConnectAsync()
      {
         if( this.RawSocket != null ) throw new InvalidOperationException(
            $"The {nameof(RawSocket)} is already created from a previous {nameof(ConnectAsync)} call. " +
            $"If you get this exception, you'll need to dispose of this {nameof(CoinbaseProWebSocket)} and create a new instance. " +
            $"Don't call {nameof(ConnectAsync)} multiple times on the same instance.");

         this.connecting = new TaskCompletionSource<bool>();

         this.RawSocket = new WebSocket(this.Config.SocketUri);
         this.RawSocket.Opened += RawSocket_Opened;
         this.RawSocket.Open();

         return this.connecting.Task;
      }

      private void RawSocket_Opened(object sender, EventArgs e)
      {
         this.connecting.SetResult(true);
      }

      public async Task SubscribeAsync(Subscription subscription)
      {
         if( this.RawSocket.State != WebSocketState.Open ) throw new InvalidOperationException("Socket must be connected.");

         subscription.ExtraJson.Add("type", JToken.FromObject(MessageType.Subscribe));

         string subJson;
         if( !string.IsNullOrWhiteSpace(this.Config.ApiKey) )
         {
            subJson = await WebSocketHelper.MakeAuthenticatedSubscriptionAsync(subscription, this.Config)
               .ConfigureAwait(false);
         }
         else
         {
            subJson = JsonConvert.SerializeObject(subscription);
         }

         this.RawSocket.Send(subJson);
      }

      public void Unsubscribe(Subscription subscription)
      {
         subscription.ExtraJson.Add("type", JToken.FromObject(MessageType.Unsubscribe));

         var json = JsonConvert.SerializeObject(subscription);

         this.RawSocket.Send(json);
      }


      public void Dispose()
      {
         this.RawSocket.Opened -= RawSocket_Opened;
         this.RawSocket?.Dispose();
      }
   }
}
