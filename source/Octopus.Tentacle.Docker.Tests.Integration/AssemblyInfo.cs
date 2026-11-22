// Every test builds its own fake server and container, so tests can run side by side. Each one mostly waits on a
// container starting, so a few at a time cuts the run time without overloading the Docker host.
[assembly: Parallelizable(ParallelScope.All)]
[assembly: LevelOfParallelism(4)]
