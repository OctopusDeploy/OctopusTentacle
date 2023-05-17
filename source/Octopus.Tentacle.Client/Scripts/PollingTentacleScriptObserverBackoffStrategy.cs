using System;

namespace Octopus.Tentacle.Client.Scripts
{
    public class PollingTentacleScriptObserverBackoffStrategy : ScriptObserverBackoffStrategy
    {
        public PollingTentacleScriptObserverBackoffStrategy() :
            base(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(5), 1.4)
        {
            // The default values - Min:300ms Max:5seconds Base:1.4 will cause a backoff of
            // Iteration 00 (~00:00:00)         - Delay: 00:00:00.3000000
            // Iteration 01 (~00:00:00.3000000) - Delay: 00:00:00.4200000
            // Iteration 02 (~00:00:00.7200000) - Delay: 00:00:00.5879999
            // Iteration 03 (~00:00:01.3079999) - Delay: 00:00:00.8231999
            // Iteration 04 (~00:00:02.1311998) - Delay: 00:00:01.1524799
            // Iteration 05 (~00:00:03.2836797) - Delay: 00:00:01.6134719
            // Iteration 06 (~00:00:04.8971516) - Delay: 00:00:02.2588607
            // Iteration 07 (~00:00:07.1560123) - Delay: 00:00:03.1624051
            // Iteration 08 (~00:00:10.3184174) - Delay: 00:00:04.4273671
            // Iteration 09 (~00:00:14.7457845) - Delay: 00:00:05
            // Iteration 10 (~00:00:19.7457845) - Delay: 00:00:05
        }
    }
}
