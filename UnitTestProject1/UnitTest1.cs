using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using BassPlayer;

namespace BassAudioEngineTest
{
    [TestClass]
    public class BassAudioEngineTest
    {
        private class TestableBassAudioEngine : BassAudioEngine
        {
            public TestableBassAudioEngine() : base() { }

            public void CallSetState(BassAudioEngine.PlayState newState)
            {
               // SetState(newState);
            }
        }

        [TestMethod]
        public void SetState_StateTransition_FiresEventWithCorrectStates()
        {
            var player = new TestableBassAudioEngine();
            var eventFired = false;
            BassAudioEngine.PlayState receivedOldState = BassAudioEngine.PlayState.Init;
            BassAudioEngine.PlayState receivedNewState = BassAudioEngine.PlayState.Init;

            player.PlaybackStateChanged += (sender, oldState, newState) =>
            {
                eventFired = true;
                receivedOldState = oldState;
                receivedNewState = newState;
            };

            Assert.AreEqual(BassAudioEngine.PlayState.Init, player.State);
            Assert.IsFalse(eventFired);

            player.CallSetState(BassAudioEngine.PlayState.Playing);

            Assert.IsTrue(eventFired);
            Assert.AreEqual(BassAudioEngine.PlayState.Init, receivedOldState);
            Assert.AreEqual(BassAudioEngine.PlayState.Playing, receivedNewState);
            Assert.AreEqual(BassAudioEngine.PlayState.Playing, player.State);
        }
    }
}