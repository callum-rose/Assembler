using Assembler.RemoteTooling;
using Assembler.RemoteTooling.Commands;

if (args.Length == 0)
{
	Usage();
	return 2;
}

var command = args[0];
var rest = args[1..];

try
{
	return command switch
	{
		"setup" => SetupCommand.Run(rest),
		"publish" => PublishCommand.Run(rest),
		"refine" => RefineCommand.Run(rest),
		"daemon" => DaemonCommand.Run(rest),
		"status" => StatusCommand.Run(rest),
		"version" or "-v" or "--version" => PrintVersion(),
		"-h" or "--help" or "help" => Usage(),
		_ => Unknown(command),
	};
}
catch (AppException ex)
{
	Console.Error.WriteLine(ex.Message);
	return 1;
}

static int PrintVersion()
{
	Console.Out.WriteLine(BuildInfo.Version);
	return 0;
}

static int Unknown(string command)
{
	Console.Error.WriteLine($"unknown command: {command}");
	Usage();
	return 2;
}

static int Usage()
{
	Console.Error.WriteLine(
		"""
        assembler-remote — dev-side tooling for the Assembler remote game pipeline.

        Usage:
          assembler-remote setup [repo-name]                    create the store repo + generate label
          assembler-remote publish "<brief>" [game-id]          generate, validate and publish a new game
          assembler-remote publish path/to/descriptor.yaml [id] publish/refresh an existing descriptor
          assembler-remote refine <game-id> "<change>"          revise a published game and bump its version
          assembler-remote daemon                               poll GitHub issues and fulfil generation requests
          assembler-remote status [--json]                      show what the daemon is generating right now
          assembler-remote version                              print the running build's version

        Configuration is via environment variables — see RemoteTooling/README.md.
        """);
	return 2;
}
