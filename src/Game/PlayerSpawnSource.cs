using System;
using PerfectRandom.Sulfur.Core;

namespace Ryuka.Sulfur.Rummage.Game
{
    /// <summary>
    /// Relays the game's own "player spawned" callback, which level generation raises once a level
    /// has been built and immediately before it sets up the level's service stations.
    ///
    /// GameManager does not exist yet when the plugin wakes up, so the subscription is made on the
    /// first frame it does exist, and moved if the game ever replaces the instance. Nothing is
    /// polled beyond that reference check.
    /// </summary>
    internal sealed class PlayerSpawnSource : IDisposable
    {
        private readonly GameManager.OnPlayerSpawned handler;
        private GameManager subscribedTo;

        public PlayerSpawnSource()
        {
            handler = Raise;
        }

        public event Action PlayerSpawned;

        public void Tick()
        {
            GameManager current = StaticInstance<GameManager>.Instance;
            if (ReferenceEquals(current, subscribedTo))
            {
                return;
            }

            Unsubscribe();

            if (current != null)
            {
                current.onPlayerSpawned += handler;
                subscribedTo = current;
            }
        }

        public void Dispose()
        {
            Unsubscribe();
            PlayerSpawned = null;
        }

        private void Unsubscribe()
        {
            if (ReferenceEquals(subscribedTo, null))
            {
                return;
            }

            // The managed delegate field is still reachable after Unity destroys the object.
            subscribedTo.onPlayerSpawned -= handler;
            subscribedTo = null;
        }

        private void Raise()
        {
            Action listeners = PlayerSpawned;
            if (listeners == null)
            {
                return;
            }

            // One failing listener must not stop the others, nor throw into level generation.
            foreach (Delegate listener in listeners.GetInvocationList())
            {
                try
                {
                    ((Action)listener)();
                }
                catch (Exception ex)
                {
                    RummagePlugin.Log.LogError("Player spawned listener " + listener.Method.DeclaringType?.Name + "."
                        + listener.Method.Name + " failed: " + ex);
                }
            }
        }
    }
}
