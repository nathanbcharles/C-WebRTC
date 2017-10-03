using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Spitfire;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Example {
  public partial class Form1 : Form {

    public Form1() {
      InitializeComponent();
    }

    public string Id { get; set; }
    public string Sdp { get; set; }
    public List<SpitfireIceCandidate> Candidates { get; set; }
    public SpitfireRtc Spitfire;
    public CancellationTokenSource Token;
    delegate void SetTextCallback(string text);

    private void button1_Click(object sender, EventArgs e) {
      Candidates = new List<SpitfireIceCandidate>();
      Spitfire = new SpitfireRtc();
      //Spitfire.AddServerConfig("stun:52.210.97.83:3478?transport=udp", string.Empty, string.Empty);

      Token = new CancellationTokenSource();
      dynamic json = JsonConvert.DeserializeObject(textBox1.Text);
      foreach (var c in json.candidates) {
        Spitfire.AddIceCandidate(c.sdpMid.ToString(), c.sdpMLineIndex.ToObject<int>(), c.candidate.ToString());
      }
      AddSession(json.sdp.sdp.ToString());
    }
    public void AddSession(string sdp) {
      using (var go = new ManualResetEvent(false)) {
        Task.Factory.StartNew(() =>
        {
         BeginLoop(go);       
        }, Token.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        if (go.WaitOne(9999))
          Setup(sdp);
      }
    }

    public void BeginLoop(ManualResetEvent go) {
      //Call this before starting a peer connection
      SpitfireRtc.InitializeSSL();
      Spitfire.AddServerConfig(new ServerConfig { Host = "52.210.97.83", Username = "dvw", Password = "helloice1", Port = 3478, Type = ServerType.Turn });
      //Spitfire.AddServerConfig("turn:52.210.97.83:3478?transport=tcp", "dvw", "helloice1");

      var started = Spitfire.InitializePeerConnection();

      if (started) {
        go.Set();
        //Keeps the RTC thread alive and active
        while (!Token.Token.IsCancellationRequested && Spitfire.ProcessMessages(1000)) {
          Spitfire.ProcessMessages(1000);
        }
        //Do cleanup here
        Console.WriteLine("WebRTC message loop is dead.");
      }
    }

    public void Setup(string sdp) {
      Spitfire.OnDataChannelOpen += DataChannelOpen;
      Spitfire.OnDataChannelClose += SpitfireOnOnDataChannelClose;
      Spitfire.OnBufferAmountChange += SpitfireOnOnBufferAmountChange;
      Spitfire.OnDataMessage += HandleMessage;
      Spitfire.OnIceStateChange += IceStateChange;
      Spitfire.OnSuccessAnswer += OnSuccessAnswer;
      Spitfire.OnIceCandidate += SpitfireOnOnIceCandidate;
      Spitfire.OnFailure += SpitFireOnFail;
      Spitfire.OnError += SpitFireOnError;
      //Gives your newly created peer connection the remote clients SDP
      Spitfire.SetOfferRequest(sdp);


    }

    private void SpitfireOnOnIceCandidate(SpitfireIceCandidate iceCandidate) {
      //var parsed = IceParser.Parse(iceCandidate.Sdp);
      //Reply to the remote client with your ICE information (sdp, sdpMid, sdpIndex)
      Console.WriteLine("NEW CANDIDATE: ", iceCandidate.Sdp, iceCandidate.SdpMid, iceCandidate.SdpIndex);
      //if(iceCandidate.Sdp.IndexOf("relay") > 0) {
        Candidates.Add(iceCandidate);
      //}
      processJson();
    }
    private void processJson() {
      JArray candidates = new JArray();

      foreach (SpitfireIceCandidate candidate in Candidates) {
        JObject candidaterow = new JObject(new JProperty("candidate", candidate.Sdp), new JProperty("sdpMid", candidate.SdpMid), new JProperty("sdpMLineIndex", candidate.SdpIndex));
        candidates.Add(candidaterow);
      }
      JObject json = new JObject(
          new JProperty("sdp", new JObject(new JProperty("type", "answer"), new JProperty("sdp", Sdp))),
          new JProperty("candidates", candidates)
      );
      SetText(json.ToString());
    }
    private void OnSuccessAnswer(SpitfireSdp sdp) {
      //reply to the remote client with your SDP
      Sdp = sdp.Sdp;
    }
    private void SpitFireOnFail(string err) {
      Console.WriteLine(err);
    }
    private void SpitFireOnError() {
      Console.WriteLine("nope");
    }

    private void IceStateChange(IceConnectionState state) {
      if (state == IceConnectionState.Disconnected) {
        Console.WriteLine("ICE has left the building.");
      }
      if (state == IceConnectionState.Failed) {
        Console.WriteLine("ICE has failed.");
      }
      if(state == IceConnectionState.Checking) {
        Console.WriteLine("ICE IS CHECKING.................");
      }
    }

    private void HandleMessage(string label, DataMessage msg) {
      if (msg.IsBinary) {
        Console.WriteLine(msg.RawData.Length);
      }
      else {
        Console.WriteLine(msg.Data);
      }
    }

    private void SpitfireOnOnBufferAmountChange(string label, int previousBufferAmount, int currentBufferAmount,
               int bytesSent,
               int bytesReceived) {
    }

    private void SpitfireOnOnDataChannelClose(string label) {
      Console.WriteLine("Data Channel Closed!");
    }

    private void DataChannelOpen(string label) {
      Console.WriteLine("$Data Channel Opened!");
    }

    private void SetText(string text) {
      // InvokeRequired required compares the thread ID of the
      // calling thread to the thread ID of the creating thread.
      // If these threads are different, it returns true.
      if (this.textBox2.InvokeRequired) {
        SetTextCallback d = new SetTextCallback(SetText);
        this.Invoke(d, new object[] { text });
      }
      else {
        this.textBox2.Text = text;
      }
    }
  }
}
