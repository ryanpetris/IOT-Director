using System.Collections.Concurrent;

namespace IotDirector.Connection
{
    public class PinState
    {
        private ConcurrentDictionary<int, int> States { get; } = new ConcurrentDictionary<int, int>();

        /// <summary>
        /// Gets the current state of the pin, or the default value (0) if the state does not exist.
        /// </summary>
        /// <param name="pin">The pin to get the state of.</param>
        /// <returns>The state of the pin.</returns>
        public int Get(int pin)
        {
            if (States.TryGetValue(pin, out var state))
                return state;

            return 0;
        }

        /// <summary>
        /// Gets the current state of the pin, or the default value (false) if the state does not exist.
        /// </summary>
        /// <param name="pin">The pin to get the state of.</param>
        /// <returns>The state of the pin.</returns>
        public bool GetBool(int pin)
        {
            return Get(pin) > 0;
        }

        /// <summary>
        /// Checks if the state of this pin exists.
        /// </summary>
        /// <param name="pin">The pin to check.</param>
        /// <returns>True if the pin state exists, False otherwise.</returns>
        public bool Has(int pin)
        {
            return States.ContainsKey(pin);
        }

        /// <summary>
        /// Sets the state of the given pin.
        /// </summary>
        /// <param name="pin">The pin to update.</param>
        /// <param name="state">The pin state.</param>
        /// <returns>True if the value changed or did not exist, False otherwise.</returns>
        public bool Set(int pin, bool state)
        {
            return Set(pin, state ? 1 : 0);
        }
        
        /// <summary>
        /// Sets the state of the given pin.
        /// </summary>
        /// <param name="pin">The pin to update.</param>
        /// <param name="state">The pin state.</param>
        /// <returns>True if the value changed or did not exist, False otherwise.</returns>
        public bool Set(int pin, int state)
        {
            var exists = States.TryGetValue(pin, out var existingState);
            var changed = !exists || existingState != state;

            if (exists)
            {
                States.TryUpdate(pin, state, existingState);
            }
            else
            {
                States.TryAdd(pin, state);
            }

            return changed;
        }
    }
}