if(args.Length!=2||args[0]!="--report")throw new ArgumentException("Use --report <JSON path>");
return AudioOutputChecks.Run(args[1]);
