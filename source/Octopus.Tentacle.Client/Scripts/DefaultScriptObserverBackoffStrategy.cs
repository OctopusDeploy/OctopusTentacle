using System;

namespace Octopus.Tentacle.Client.Scripts
{
    public class DefaultScriptObserverBackoffStrategy : ScriptObserverBackoffStrategy
    {
        public DefaultScriptObserverBackoffStrategy() :
            base(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(5), 1.15)
        {
            // The default values - Min:300ms Max:5seconds Base:1.15 will cause a backoff of
            // Iteration 00 (~00:00:00)         - Delay: 00:00:00.3000000
            // Iteration 01 (~00:00:00.3000000) - Delay: 00:00:00.3450000
            // Iteration 02 (~00:00:00.6450000) - Delay: 00:00:00.3967499
            // Iteration 03 (~00:00:01.0417499) - Delay: 00:00:00.4562624
            // Iteration 04 (~00:00:01.4980123) - Delay: 00:00:00.5247018
            // Iteration 05 (~00:00:02.0227141) - Delay: 00:00:00.6034071
            // Iteration 06 (~00:00:02.6261212) - Delay: 00:00:00.6939182
            // Iteration 07 (~00:00:03.3200394) - Delay: 00:00:00.7980059
            // Iteration 08 (~00:00:04.1180453) - Delay: 00:00:00.9177068
            // Iteration 09 (~00:00:05.0357521) - Delay: 00:00:01.0553628
            // Iteration 10 (~00:00:06.0911149) - Delay: 00:00:01.2136673
            // Iteration 11 (~00:00:07.3047822) - Delay: 00:00:01.3957174
            // Iteration 12 (~00:00:08.7004996) - Delay: 00:00:01.6050750
            // Iteration 13 (~00:00:10.3055746) - Delay: 00:00:01.8458362
            // Iteration 14 (~00:00:12.1514108) - Delay: 00:00:02.1227117
            // Iteration 15 (~00:00:14.2741225) - Delay: 00:00:02.4411184
            // Iteration 16 (~00:00:16.7152409) - Delay: 00:00:02.8072862
            // Iteration 17 (~00:00:19.5225271) - Delay: 00:00:03.2283792
            // Iteration 18 (~00:00:22.7509063) - Delay: 00:00:03.7126360
            // Iteration 19 (~00:00:26.4635423) - Delay: 00:00:04.2695314
            // Iteration 20 (~00:00:30.7330737) - Delay: 00:00:04.9099612
            // Iteration 21 (~00:00:35.6430349) - Delay: 00:00:05
            // Iteration 22 (~00:00:40.6430349) - Delay: 00:00:05
        }
    }
}
