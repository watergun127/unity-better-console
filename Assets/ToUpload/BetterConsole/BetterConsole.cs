﻿using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.ComponentModel;
using UnityEngine;
using TurboTurnip.Utility;

namespace TurboTurnip.BetterConsole{

//This contains all data about an argument of a ConsoleCommand, i.e. type and name.
public struct ConsoleArgument{
	public Type argType;
	public string argName; 
	public bool optional,isParams;

	public ConsoleArgument(Type t,string n,bool o=false,bool p=false){
		argType=t;
		argName=n;
		optional=o;
		isParams=p;
	}
}

//This is the type of function that can be called by the console
public delegate bool ConsoleCommandCallback(List<object> arguments);

public class ConsoleCommand : IComparable{
	public string commandName,helpString;
	public ConsoleArgument[] arguments;
	public ConsoleCommandCallback callback;
	int minRequiredArgs,maxArgs;

	public ConsoleCommand(string name, ConsoleCommandCallback c,string explanation,params ConsoleArgument[] args){
		arguments=args;

		minRequiredArgs=0;
		for(int i=0;i<args.Length;i++){
			if (args[i].optional)
				break;
			minRequiredArgs++;
		}

		maxArgs=args.Length;
		if (args[args.Length-1].isParams)
			maxArgs=-1;
		commandName=name;
		callback=c;
		helpString=commandName+" ";
		foreach(ConsoleArgument a in args){
			helpString+="["+a.argName+" <"+(a.optional?"optional ":"")+(a.isParams?"params ":"")+a.argType+">]";
		}
		helpString+="\n\t"+explanation;
	}

	//Take an array of the command name + arguments and call the command function with the arguments converted to required forms.
	public bool ParseCommand(string[] givenArgs){
		if (givenArgs.Length==1 && minRequiredArgs!=0){ //If we haven't added arguments and the command requires arguments, tell the user what to give to this command.
			Debug.Log(helpString);
			return true;
		}

		//If we haven't given the correct amount of arguments, let the user know 
		if (givenArgs.Length<minRequiredArgs+1){
			Debug.LogError(commandName+" requires at least "+minRequiredArgs+" commands, given "+(givenArgs.Length-1)+".");
			return false;
		}else if (givenArgs.Length>maxArgs+1 && maxArgs>=0){
			Debug.LogError(commandName+" was given too many arguments, maximum "+maxArgs);
		}

		List<object> toSend=new List<object>();

		Type type=arguments[0].argType;
		for(int i=1;i<givenArgs.Length;i++){
			if (i-1<arguments.Length)
				type=arguments[i-1].argType;
			toSend.Add(ConvertUtil.ConvertString(givenArgs[i],type));
		}

		return callback(toSend);
	}

	//This function is for sorting ConsoleCommand objects alphabetically. 
	public int CompareTo(object cmp){
		if (cmp==null) return 1;
		return this.commandName.CompareTo(((ConsoleCommand)cmp).commandName);
	}
}

//This class registers default commands like translate, echo and egg.
static class BetterConsoleDefaultCommands{
	public static void RegisterCommands(){
		BetterConsole.RegisterCommand("translate",TranslateCommand,"Translates to_move by move_delta",new ConsoleArgument(typeof(Transform),"to_move"),new ConsoleArgument(typeof(Vector3),"move_delta"));
		BetterConsole.RegisterCommand("echo",Echo,"Logs to_echo into the console",new ConsoleArgument(typeof(string),"to_echo",false,true));
		BetterConsole.RegisterCommand("egg",Egg,"Eggs to_egg, optionally to_egg will be egged by egger",new ConsoleArgument(typeof(GameObject),"to_egg",false),new ConsoleArgument(typeof(GameObject),"egger",true));
		BetterConsole.RegisterCommand("get_var",GetVar,"Prints the value of the variable named var_name in the Component of type comp_type attached to GameObject get_obj",new ConsoleArgument(typeof(GameObject),"get_obj"),new ConsoleArgument(typeof(string),"comp_type"),new ConsoleArgument(typeof(string),"var_name"));
		BetterConsole.RegisterCommand("set_var",SetVar,"Sets set_obj's Component of type comp_type's variable named var_name to value var_val ",new ConsoleArgument(typeof(GameObject),"set_obj"),new ConsoleArgument(typeof(string),"comp_type"),new ConsoleArgument(typeof(string),"var_name"),new ConsoleArgument(typeof(string),"var_val"));
		BetterConsole.RegisterCommand("call_func",CallMethod,"Calls the function to_call on the Component of type comp_type attached to GameObject call_obj",new ConsoleArgument(typeof(GameObject),"call_obj"),new ConsoleArgument(typeof(string),"comp_type"),new ConsoleArgument(typeof(string),"to_call"),new ConsoleArgument(typeof(string),"args",true,true));
		BetterConsole.RegisterCommand("tree",Tree,"Lists either all objects in the heirarchy or all children of root_obj",new ConsoleArgument(typeof(Transform),"root_obj",true));
		BetterConsole.RegisterCommand("help",Help,"Describes the command named cmd_name",new ConsoleArgument(typeof(string),"cmd_name"));
	}

	static bool Help(List<object> args){
		ConsoleCommand cmd=BetterConsole.FindCommand((string)args[0]);
		if (cmd==null){
			Debug.LogError("Command \""+args[0]+"\" doesn't exist");
			return false;
		}
		Debug.Log(cmd.helpString);
		return true;
	}

	static bool Tree(List<object> args){
		GameObject[] roots;
		if (args.Count>0){
			roots=new GameObject[((Transform)args[0]).childCount];
			for (int i=0;i<roots.Length;i++)
				roots[i]=((Transform)args[0]).GetChild(i).gameObject;
		}else roots=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
		foreach(GameObject root in roots)
			PrintChildren(root.transform);
		return true;
	} 

	static void PrintChildren(Transform rootT){
		Debug.Log(ConvertUtil.TransformToString(rootT));
		foreach(Transform child in rootT){
			PrintChildren(child);
		}
	}	

	static bool GetVar(List<object> args){
		GameObject getFromObject=(GameObject)args[0];
		string toGet=(string)args[2];
		UnityEngine.Component getFromComponent=getFromObject.GetComponent((string)args[1]);
		Type componentType=getFromComponent.GetType();

		FieldInfo varInfo = componentType.GetField(toGet);
		if (varInfo==null){
			PropertyInfo propInfo=componentType.GetProperty(toGet);
			if (propInfo==null){
				Debug.LogError("Couldn't find field or property named "+toGet+" in "+args[1]);
				return false;
			}
			Debug.Log(getFromComponent+"."+toGet+" = "+propInfo.GetValue(getFromComponent,null));
			return true;
		}
		Debug.Log(getFromComponent+"."+toGet+" = "+varInfo.GetValue(getFromComponent));
		return true;
	}

	static bool SetVar(List<object> args){
		GameObject setToObject=(GameObject)args[0];
		string toGet=(string)args[2];
		UnityEngine.Component setToComponent=setToObject.GetComponent((string)args[1]);
		Type componentType=setToComponent.GetType();

		FieldInfo varInfo = componentType.GetField(toGet);
		if (varInfo==null){
			PropertyInfo propInfo=componentType.GetProperty(toGet);
			if (propInfo==null){
				Debug.LogError("Couldn't find field or property named "+toGet+" in "+args[1]);
				return false;
			}
			propInfo.SetValue(setToComponent,ConvertUtil.ConvertString((string)args[3],propInfo.PropertyType),null);
			return true;
		}
		varInfo.SetValue(setToComponent,ConvertUtil.ConvertString((string)args[3],varInfo.FieldType));
		return true;
	}

	static bool CallMethod(List<object> args){
		GameObject callObject=(GameObject)args[0];
		string toGet=(string)args[2];
		UnityEngine.Component callComponent=callObject.GetComponent((string)args[1]);
		Type componentType=callComponent.GetType();

		MethodInfo methodInfo=componentType.GetMethod(toGet, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (methodInfo==null){
			Debug.LogError("Couldn't find a function named "+toGet+" in "+args[1]);
			return false;
		}

		ParameterInfo[] paramInfo=methodInfo.GetParameters();
		if (args.Count==3 && paramInfo.Length>0){
			string toPrint=componentType+"."+toGet+"(";
			for(int i=0;i<paramInfo.Length-1;i++)
				toPrint+=paramInfo[i].ParameterType+" "+paramInfo[i].Name+",";
			if (paramInfo.Length>0) toPrint+=paramInfo[paramInfo.Length-1].ParameterType+" "+paramInfo[paramInfo.Length-1].Name+")";
			//else toPrint+="void)";
			Debug.Log(toPrint);
			return true;
		}

		object[] parameters=(paramInfo.Length>0)?new object[args.Count-3]:null;
		if (paramInfo.Length>0) args.CopyTo(3,parameters,0,parameters.Length);
		methodInfo.Invoke(callComponent,parameters);
		return true;
	}

	public static bool Egg(List<object> args){
		if (args.Count==1)
			Debug.Log(((GameObject)args[0]).name+" WAS EGGED");
		else 
			Debug.Log(((GameObject)args[0]).name+" WAS EGGED BY "+((GameObject)args[1]).name);
		return true;
	}

	public static bool Echo(List<object> args){
		foreach (object a in args)
			Debug.Log((string)a);
		return true;
	}

	public static bool TranslateCommand(List<object> args){
		Transform toMove=args[0] as Transform;
		Vector3 delta=(Vector3)args[1];
		toMove.position+=delta;
		return true;
	}
}

public static class BetterConsole {
	public struct Line{
		public int type;
		public string text,traceback;
		public Line(string t,string tr){
			type=0;
			text=t;
			traceback=tr;
		}
		public Line(int ty,string t,string tr){
			type=ty;
			text=t;
			traceback=tr;
		}
	}

	/*
		BACKEND
				 */

	static List<Line> logQueue;
	static List<ConsoleCommand> commands;
	public static int maxLines,maxRememberedCommands;
	static bool inited=false,allowCollapse;

	//This inits the console
	public static void LoadParams(){
		logQueue=new List<BetterConsole.Line>();
		Dictionary<string,Dictionary<string,string>> parsedINI=IniParse.ParseINI((Resources.Load("BetterConsoleConfig") as TextAsset).text);
		maxLines=int.Parse(parsedINI["Config"]["MaxLines"]);
		maxRememberedCommands=int.Parse(parsedINI["Config"]["MaxRememberedCommands"]);
		commands=new List<ConsoleCommand>();
		Application.logMessageReceived+=HandleUnityLog;
		inited=true;
		BetterConsoleDefaultCommands.RegisterCommands();
	}

	//This adds a Line to the queue, removing an old one if necessary
	static void PushLine(Line toLog){
		if (!inited) LoadParams();
		logQueue.Add(toLog);
		if (logQueue.Count>maxLines && maxLines>0)
			logQueue.RemoveAt(0);
	}

	//This returns a rich text string of the logQueue
	public static string ConsolidateLines(){
		if (!inited) LoadParams();
		string lines="";
		foreach(Line l in logQueue){
			if (l.type==1)
				lines+="<color=yellow>";
			else if (l.type==2)
				lines+="<color=red>";
			lines+=l.text;
			if (l.type!=0)
				lines+="</color>";
			lines+="\n";
		}
		lines=lines.Trim();
		return lines;
	}

	/*
		COMMANDS
				  */

	//This registers a premade ConsoleCommand
	public static bool RegisterCommand(ConsoleCommand toAdd){
		if (!inited) LoadParams();
		foreach(ConsoleCommand c in commands){
			if (c.commandName==toAdd.commandName)
				return false;
		}
		commands.Add(toAdd);
		commands.Sort();
		Debug.Log("Successfully added command "+toAdd.commandName);
		return true;
	}

	//This creates and registers a ConsoleCommand
	public static bool RegisterCommand(string commandName,ConsoleCommandCallback callback,string explanation,params ConsoleArgument[] args){
		if (!inited) LoadParams();
		foreach(ConsoleCommand c in commands){
			if (c.commandName==commandName)
				return false;
		}
		ConsoleCommand toAdd=new ConsoleCommand(commandName,callback,explanation,args);
		commands.Add(toAdd);
		commands.Sort();
		Debug.Log("Successfully added command "+toAdd.commandName);
		return true;
	}

	//This function takes a command string and parses it, returning true if the command succeeded.
	public static bool ParseCommand(string fullCommand){
		if (!inited) LoadParams();
		Debug.Log(":"+fullCommand);//Log what's being run to the console

		string[] splitCommand=SplitCommand(fullCommand); //Split the command into the command name and arguments
		/*foreach (string s in splitCommand)
			Debug.Log(s);*/ //Uncomment this to print the result of SplitCommand
		if (splitCommand.Length==0) return false; //If you didn't type anything, don't bother looking for commands to run.

		//Look for commands to run
		foreach(ConsoleCommand c in commands){
			if (c.commandName==splitCommand[0])
				return c.ParseCommand(splitCommand);
		}

		//We didn't find any commands that match the command the user wants
		Debug.LogError("That command doesn't exist!");
		return false;
	}

	//This takes the previous command and autocomplete strings and returns new ones.
	public static string[] AutoComplete(string fullCommand,string previousAutocomplete){
		if (!inited) LoadParams();

		string[] splitCommand=SplitCommand(fullCommand); //Split the command into the command name and arguments
		string finalArg=(splitCommand.Length>=1)?splitCommand[splitCommand.Length-1]:""; //This helper variable is the final string in the command.
		List<string> autoCompleteChoices=new List<string>(); //This list contains everything we could autocomplete to.

		string[] toReturn=new string[2]; //Output the new command and autocomplete (we need to be able to add quotes to the command, which is why we return both)
		toReturn[0]=fullCommand;
		toReturn[1]="";

		foreach(ConsoleCommand c in commands){
			if (splitCommand.Length<=1){ //Are we autocompleting a command or an argument
				autoCompleteChoices.Add(c.commandName);
				continue;
			}

			if (c.commandName==splitCommand[0] && splitCommand.Length<=c.arguments.Length+1){
				Type argType=c.arguments[splitCommand.Length-2].argType;
				//Debug.Log()
				if ((argType==typeof(Transform) || argType==typeof(GameObject) || argType==typeof(MonoBehaviour)) && (!fullCommand.Substring(fullCommand.Length-3,2).Contains("\"")||finalArg.Length<3)){
					//Autocomplete for a transform
						//Find the starting transform
					Transform t = ConvertUtil.StringToTransform(finalArg); //Assume finalArg is a valid Transform, function returns null otherwise
					string parentName=finalArg;
					if (t==null){
						//finalArg wasn't a valid transform
						parentName=FindParentTransformNameFromString(finalArg); //Removes all characters after the final /
						t=ConvertUtil.StringToTransform(parentName); //Converts to the parent
					}

					if (t==null){
						//Search through top level
						GameObject[] roots=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
						foreach(GameObject root in roots){
							autoCompleteChoices.Add(root.name);
						}
					}else{
						//Search through children
						if (parentName[parentName.Length-1]!='/')
							parentName+='/';
						autoCompleteChoices.Add("");
						foreach(Transform child in t){
							autoCompleteChoices.Add(parentName+child.gameObject.name);
						}
					}
				}
				//We can only autocomplete Transforms and GameObjects
				break;
			}
		}

		//Look in autoCompleteChoices for things we can autocomplete to, choose either the next in the list rel. to previousAutocomplete or the first in the list. 
		string name;
		for(int i=0;i<autoCompleteChoices.Count;i++){
			name=autoCompleteChoices[i];
			if (finalArg.Length<=name.Length && name.Substring(0,finalArg.Length)==finalArg && name!=finalArg+previousAutocomplete){
				//This can theoretically be something to autocomplete to
				//Debug.Log(name+" up for consideration "+i);
				
				//If the previous autocomplete choice was the last item in the list OR we haven't autocompleted yet, autocomplete to this item.
				if ((i==0 && finalArg+previousAutocomplete.Replace("\"","")==autoCompleteChoices[autoCompleteChoices.Count-1]) || (i>0 && finalArg+previousAutocomplete.Replace("\"","")==autoCompleteChoices[i-1]) || previousAutocomplete==""){
					toReturn[1]=name.Substring(finalArg.Length);
					if (fullCommand.Length==finalArg.Length) break; //If we're autocompleting a command, we don't need any quotes

					bool hasLeftEndQuotes=fullCommand.Substring(fullCommand.Length-finalArg.Length-1,finalArg.Length).Contains("\"");
					if (name.Contains(" ")){
						//We need to make sure this argument is encapsulated with quotes
						if (name[name.Length-1]!='\"')
							toReturn[1]+="\""; //Add quotes on the right end if they aren't already there

						fullCommand=fullCommand.Trim();
						if (!hasLeftEndQuotes) //If finalArg doesn't have quotes on the left end already, add some
							toReturn[0]=fullCommand.Substring(0,fullCommand.Length-1-finalArg.Length)+" \""+finalArg;
					}else if (hasLeftEndQuotes){
						toReturn[0]=fullCommand.Substring(0,fullCommand.Length-1-finalArg.Length)+finalArg;
					}
					break;
				}
			}
		}
		return toReturn;
	}

	public static ConsoleCommand FindCommand(string name){
		//Look for commands to run
		foreach(ConsoleCommand c in commands){
			if (c.commandName==name)
				return c;
		}
		return null;
	}

	//This removes everything in the string after the final / (i.e. "Cube/Sp" -> "Cube/")
	static string FindParentTransformNameFromString(string s){
		for(int i=s.Length-1;i>=0;i--)
			if (s[i]=='/') return s.Substring(0,i+1);
		return s;
	}

	//This splits a string into a command and a set of arguments (i.e. "translate "Cube/Child In Space" (1,0,0)" -> ["translate","Cube/Child In Space","(1,0,0)"])
	static string[] SplitCommand(string fullCommand,bool trimSpaces=true){
		List<string> splitCommand=new List<string>();
		bool enclosed=false; //This represents whether the current character is enclosed in () or ""
		int start=0; //This represents the index where the enclosure began
		char encloser=' ';
		for(int i=0;i<fullCommand.Length;i++){
			//If this char is an unenclosed space or if it finished enclosure, we've found a command/argument
			if ((fullCommand[i]==' '&& !enclosed) || (enclosed&&((fullCommand[i]==')'&&encloser=='(')	 || fullCommand[i]==encloser))){
				if (!enclosed){
					splitCommand.Add(fullCommand.Substring(start,i-start+1));
					start=i+1;
				}else if (fullCommand[start]=='\"'){
					splitCommand.Add(fullCommand.Substring(start+1,i-start-1));
					start=i+2;
				}else{
					splitCommand.Add(fullCommand.Substring(start-1,i-start+2)); //This makes sure we capture both parentheses in the string
					start=i+2;
				}
				enclosed=false;
			}else if (!enclosed&&(fullCommand[i]=='(' || fullCommand[i]=='\"')){ //Otherwise, if this char starts enclosure we should set the enclosure flag
				enclosed=true;
				encloser=fullCommand[i];
			}
		}

		if (start<fullCommand.Length) { //If we haven't added the final argument/command, do so
			if (fullCommand[start]=='\"') splitCommand.Add(fullCommand.Substring(start,fullCommand.Length-start).Replace("\"","")); //If it's enclosed by quotes, capture the bits inbetween the quotes 
			else if (start+1<fullCommand.Length && fullCommand[start+1]=='(') splitCommand.Add(fullCommand.Substring(start-1,fullCommand.Length-start)); //If it's enclosed by parentheses, capture the bits inbetween the parentheses including the parentheses
			else splitCommand.Add(fullCommand.Substring(start,fullCommand.Length-start)); //Otherwise just take it normally
		}

		//Trim whitespace from front and back of every string
		if (trimSpaces){
			for(int i=0;i<splitCommand.Count;i++)
				splitCommand[i]=splitCommand[i].Trim();
		}

		//Remove empties
		splitCommand.RemoveAll(String.IsNullOrEmpty);

		return splitCommand.ToArray();
	}

	/*				
		LOGGING  
				 */

	//These functions all push new lines to the console, but mark the lines as warning or error lines depending on the function called.
	static string Log(string strToLog,string traceback=""){
		Line toLog=new Line(strToLog,traceback);
		PushLine(toLog);
		return strToLog;
	}

	static string LogWarning(string strToLog,string traceback=""){
		Line toLog=new Line(1,strToLog,traceback);
		PushLine(toLog);
		return strToLog;
	}

	static string LogError(string strToLog,string traceback=""){
		Line toLog=new Line(2,strToLog,traceback);
		PushLine(toLog);
		return strToLog;
	}

	//This is the function that intercepts Debug.Log/LogWarning/LogError calls, and routes them to the appropriate Log function
	static void HandleUnityLog(string logString, string stackTrace, LogType type){
		if (type==LogType.Error)
			LogError(logString,stackTrace);
		else if (type==LogType.Warning)
			LogWarning(logString,stackTrace);
		else
			Log(logString,stackTrace);
	}

}

}