﻿using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Grouper2.GPPAssess
{
    public partial class AssessGpp
    {
        private JObject GetAssessedScheduledTasks(JObject gppCategory)
        {
            JObject assessedGppSchedTasksAllJson = new JObject();

            //Console.WriteLine("");
            //Utility.DebugWrite(gppCategory.ToString());
            //Console.WriteLine("");

            List<string> schedTaskTypes = new List<string> {"Task", "TaskV2", "ImmediateTask", "ImmediateTaskV2"};

            foreach (string schedTaskType in schedTaskTypes)
            {
                if (gppCategory[schedTaskType] is JArray)
                {
                    foreach (JToken taskJToken in gppCategory[schedTaskType])
                    {
                        JObject assessedGppSchedTask = GetAssessedScheduledTask(taskJToken, schedTaskType);
                        if (assessedGppSchedTask != null)
                        {
                            assessedGppSchedTasksAllJson.Add(assessedGppSchedTask["UID"].ToString(),
                                assessedGppSchedTask);
                        }
                    }
                }
                else if (gppCategory[schedTaskType] is JObject)
                {
                    JObject assessedGppSchedTask = GetAssessedScheduledTask(gppCategory[schedTaskType], schedTaskType);
                    if (assessedGppSchedTask != null)
                    {
                        assessedGppSchedTasksAllJson.Add(assessedGppSchedTask["UID"].ToString(), assessedGppSchedTask);
                    }
                }
            }

            if (assessedGppSchedTasksAllJson.HasValues)
            {
                return assessedGppSchedTasksAllJson;
            }
            else
            {
                return null;
            }
        }

        private JProperty ExtractCommandFromScheduledTask(JToken scheduledTask, ref int interestLevel, int number = 0)
        {
            string commandString = Utility.GetSafeString(scheduledTask, "Command");
            string argumentsString = Utility.GetSafeString(scheduledTask, "Arguments");

            JObject command = new JObject(new JProperty("Command", commandString));
            JObject arguments = new JObject(new JProperty("Arguments", argumentsString));

            command = FileSystem.InvestigatePath(commandString);
            arguments = Utility.InvestigateString(argumentsString);
            if ((arguments != null) && (arguments["InterestLevel"] != null))
            {
                int argumentInterest = (int)arguments["InterestLevel"];
                interestLevel = interestLevel + argumentInterest;
            }

            if ((command != null) && (command["InterestLevel"] != null))
            {
                int commandInterest = (int)command["InterestLevel"];
                interestLevel = interestLevel + commandInterest;
            }

            string caption = "Exec";
            if (number > 0)
            {
                caption += " - " + number;
            }
            return new JProperty(caption, new JObject(
                        new JProperty("Command", command),
                        new JProperty("Args", arguments)
                    )
                );
        }

        private JObject GetAssessedScheduledTask(JToken scheduledTask, string schedTaskType)
        {
            int interestLevel = 4;

            JObject assessedScheduledTask = new JObject
            {
                {"Name", scheduledTask["@name"].ToString()},
                {"UID", scheduledTask["@uid"].ToString()},
                {"Type", schedTaskType},
                {"Changed", scheduledTask["@changed"].ToString()}
            };

            if (scheduledTask["Properties"]["@runAs"] != null)
            {
                assessedScheduledTask.Add("Run As", Utility.GetSafeString(scheduledTask["Properties"], "@runAs"));
            }

            string cPassword = Utility.GetSafeString(scheduledTask["Properties"], "@cpassword");
            if (cPassword.Length > 1)
            {
                assessedScheduledTask.Add("Encrypted Password",
                    Utility.GetSafeString(scheduledTask["Properties"], "@cpassword"));
                assessedScheduledTask.Add("Decrypted Password", Utility.DecryptCpassword(cPassword));
                interestLevel = 10;
            }

            if (scheduledTask["Properties"]["@logonType"] != null)
            {
                assessedScheduledTask.Add("Logon Type",
                    Utility.GetSafeString(scheduledTask["Properties"], "@logonType"));
            }

            // handle the entries that are specific to some task types but not others
            // both taskv2 and immediatetaskv2 have the same rough structure
            if (schedTaskType.EndsWith("V2"))
            {
                assessedScheduledTask.Add("Action",
                    Utility.GetActionString(scheduledTask["Properties"]["@action"].ToString()));
                assessedScheduledTask.Add("Description", Utility.GetSafeString(scheduledTask, "@desc"));
                assessedScheduledTask.Add("Enabled",
                    Utility.GetSafeString(scheduledTask["Properties"]["Task"]["Settings"], "Enabled"));
                // just adding the Triggers info raw, there are way too many options.
                assessedScheduledTask.Add("Triggers", scheduledTask["Properties"]["Task"]["Triggers"]);

                if (scheduledTask["Properties"]["Task"]["Actions"]["ShowMessage"] != null)
                {
                    assessedScheduledTask.Add(
                        new JProperty("Action - Show Message", new JObject(
                                new JProperty("Title",
                                    Utility.GetSafeString(scheduledTask["Properties"]["Task"]["Actions"]["ShowMessage"],
                                        "Title")),
                                new JProperty("Body",
                                    Utility.GetSafeString(scheduledTask["Properties"]["Task"]["Actions"]["ShowMessage"],
                                        "Body"))
                            )
                        )
                    );
                }

                if (scheduledTask["Properties"]["Task"]["Actions"]["Exec"] != null)
                {
                    // do we have an array of Command?
                    if (scheduledTask["Properties"]["Task"]["Actions"]["Exec"].Type == JTokenType.Array)
                    {
                        int i = 1;
                        foreach (JToken item in scheduledTask["Properties"]["Task"]["Actions"]["Exec"])
                        {
                            assessedScheduledTask.Add(ExtractCommandFromScheduledTask(item, ref interestLevel, i));
                            i++;
                        }
                    }
                    else
                    {
                        // or just one?
                        assessedScheduledTask.Add(ExtractCommandFromScheduledTask(scheduledTask["Properties"]["Task"]["Actions"]["Exec"], ref interestLevel));
                    }
                }

                if (scheduledTask["Properties"]["Task"]["Actions"]["SendEmail"] != null)
                {
                    string attachmentString =
                        Utility.GetSafeString(
                            scheduledTask["Properties"]["Task"]["Actions"]["SendEmail"]["Attachments"], "File");

                    JObject attachment = new JObject(new JProperty("Attachment", attachmentString));

                    if (GlobalVar.OnlineChecks)
                    {
                        attachment = Utility.InvestigateString(attachmentString);
                        if (attachment["InterestLevel"] != null)
                        {
                            int attachmentInterest = (int) attachment["InterestLevel"];
                            interestLevel = interestLevel + attachmentInterest;
                        }
                    }

                    assessedScheduledTask.Add(
                        new JProperty("Action - Send Email", new JObject(
                                new JProperty("From",
                                    Utility.GetSafeString(scheduledTask["Properties"]["Task"]["Actions"]["SendEmail"],
                                        "From")),
                                new JProperty("To",
                                    Utility.GetSafeString(scheduledTask["Properties"]["Task"]["Actions"]["SendEmail"],
                                        "To")),
                                new JProperty("Subject",
                                    Utility.GetSafeString(scheduledTask["Properties"]["Task"]["Actions"]["SendEmail"],
                                        "Subject")),
                                new JProperty("Body",
                                    Utility.GetSafeString(scheduledTask["Properties"]["Task"]["Actions"]["SendEmail"],
                                        "Body")),
                                new JProperty("Header Fields",
                                    Utility.GetSafeString(scheduledTask["Properties"]["Task"]["Actions"]["SendEmail"],
                                        "HeaderFields")),
                                new JProperty("Attachment", attachment),
                                new JProperty("Server",
                                    Utility.GetSafeString(scheduledTask["Properties"]["Task"]["Actions"]["SendEmail"],
                                        "Server"))
                            )
                        )
                    );
                }
            }

            if (schedTaskType == "Task")
            {
                string commandString = Utility.GetSafeString(scheduledTask["Properties"], "@appname");
                string argumentsString = Utility.GetSafeString(scheduledTask["Properties"], "@args");
                JObject command = new JObject(new JProperty("Command", commandString));
                JObject arguments = new JObject(new JProperty("Arguments", argumentsString));
                
                command = FileSystem.InvestigatePath(commandString);
                arguments = Utility.InvestigateString(argumentsString);

                if ((arguments != null) && (arguments["InterestLevel"] != null))
                {
                    int argumentInterest = (int) arguments["InterestLevel"];
                    interestLevel = interestLevel + argumentInterest;
                }

                if ((command != null) && (command["InterestLevel"] != null))
                {
                    int commandInterest = (int) command["InterestLevel"];
                    interestLevel = interestLevel + commandInterest;
                }
                

                assessedScheduledTask.Add("Action",
                    Utility.GetActionString(scheduledTask["Properties"]["@action"].ToString()));
                assessedScheduledTask.Add("Command", command);
                assessedScheduledTask.Add("Args", arguments);
                JObject assessedWorkingDir =
                    FileSystem.InvestigatePath(Utility.GetSafeString(scheduledTask["Properties"], "@startIn"));
                if ((assessedWorkingDir != null) && assessedWorkingDir.HasValues)
                {
                    assessedScheduledTask.Add("Working Dir", assessedWorkingDir);
                }
                
                if (scheduledTask["Properties"]["Triggers"] != null)
                {
                    assessedScheduledTask.Add("Triggers", scheduledTask["Properties"]["Triggers"]);
                }
            }

            if (schedTaskType == "ImmediateTask")
            {
                string argumentsString = Utility.GetSafeString(scheduledTask["Properties"], "@args");
                string commandString = Utility.GetSafeString(scheduledTask["Properties"], "@appName");
                JObject command = new JObject(new JProperty("Command", commandString));
                JObject arguments = new JObject(new JProperty("Arguments", argumentsString));

                
                command = FileSystem.InvestigatePath(commandString);
                arguments = Utility.InvestigateString(argumentsString);

                if ((arguments != null) && (arguments["InterestLevel"] != null))
                {
                    int argumentInterest = (int) arguments["InterestLevel"];
                    interestLevel = interestLevel + argumentInterest;
                }

                if ((command != null) && (command["InterestLevel"] != null))
                {
                    int commandInterest = (int) command["InterestLevel"];
                    interestLevel = interestLevel + commandInterest;
                }
                

                assessedScheduledTask.Add("Command", command);
                assessedScheduledTask.Add("Arguments", arguments);

                JObject assessedWorkingDir =
                    FileSystem.InvestigatePath(Utility.GetSafeString(scheduledTask["Properties"], "@startIn"));
                if ((assessedWorkingDir != null) && assessedWorkingDir.HasValues)
                {
                    assessedScheduledTask.Add("Working Dir", assessedWorkingDir);
                }

                assessedScheduledTask.Add("Comment", Utility.GetSafeString(scheduledTask["Properties"], "@comment"));
            }

            if (interestLevel < GlobalVar.IntLevelToShow)
            {
                return null;
            }

            return assessedScheduledTask;
        }
    }
}
