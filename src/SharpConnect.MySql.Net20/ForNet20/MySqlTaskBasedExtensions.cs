﻿//MIT, 2016, brezza92, EngineKit and contributors 

using System;
using System.Collections.Generic;

namespace SharpConnect.MySql.BasicAsyncTasks
{
    //------------------------------------
    //this sample is designed for .net2.0 
    //that dose not have Task library
    //but we intend to design the api different from 
    //original TAP model
    //here:
    //we pass TaskChain as an arg to async method
    //and make sure that the task is add into the taskchain
    //------------------------------------
    public static class MySqlTaskBasedExtension
    {
        public static ActionTask OpenAsync(this MySqlConnection conn, TaskChain ch)
        {
            return ch.AddTask(() =>
            {
                //open connection async
                //after finish then call next task in task chain
                conn.Open(ch.Next);
            });
        }
        public static ActionTask CloseAsync(this MySqlConnection conn, TaskChain ch)
        {
            return ch.AddTask(() =>
            {
                conn.Close(ch.Next);
            });
        }
        //------------------------------------------------------------
        public static ActionTask PrepareAsync(this MySqlCommand cmd, TaskChain ch)
        {
            return ch.AddTask(() =>
            {
                cmd.Prepare(ch.Next);
            });

        }
        public static ActionTask ExecuteNonQueryAsync(this MySqlCommand cmd, TaskChain ch)
        {
            return ch.AddTask(() =>
            {
                cmd.ExecuteNonQuery(ch.Next);
            });
        }

        public static ActionTask ExecuteReaderAsync(this MySqlCommand cmd, TaskChain ch, Action<MySqlDataReader> readerReady)
        {
            return ch.AddTask(() =>
            {
                cmd.ExecuteReader(reader =>
                {
                    readerReady(reader);
                    //
                    ch.Next();
                });
            });

        }

        public static ActionTask ExecuteScalarAsync(this MySqlCommand cmd, TaskChain ch, Action<object> resultReady)
        {
            return ch.AddTask(() =>
            {
                cmd.ExecuteScalar(result =>
                {
                    resultReady(result);
                    ch.Next();
                });
            });
        }
        //-----------------------------------------------------------------------------
        public static ActionTask CloseAsync(this MySqlDataReader reader, TaskChain ch)
        {
            return ch.AddTask(() =>
            {
                reader.Close(ch.Next);
            });
        }
    }

    public abstract class BasicTaskBase
    {
        public BasicTaskBase()
        {
        }
        public TaskChain OwnerTaskChain
        {
            get;
            set;
        }
        public abstract void Start();
        public void Wait()
        {
            //wait until this task complete
        }
        public void StartAndWait()
        {

        }

        //user can assign name for this task
        //mainly purpose for debuging
        public string Name { get; set; }

    }

    public enum TaskStatus
    {
        Init,
        Running,
        Finish
    }

    public class ActionTask : BasicTaskBase
    {
        Action action;
        TaskStatus taskStatus;
        public ActionTask(Action action)
        {
            this.action = action;
        }
        public TaskStatus Status
        {
            get { return taskStatus; }
        }
        public override void Start()
        {
            //each task must run once ***
            switch (taskStatus)
            {
                case TaskStatus.Init:
                    taskStatus = TaskStatus.Running;
                    action();
                    taskStatus = TaskStatus.Finish;
                    break;
                default:
                    throw new NotSupportedException();
            }

        }
    }

    public class TaskChain
    {
        int currentIndex = 0;
        Action onFinish;
        Action onBeginTask;
        bool pleaseStop;
        List<BasicTaskBase> taskList = new List<BasicTaskBase>();

        public ActionTask AddTask(Action a)
        {
            ActionTask actionTask = new ActionTask(a);
            actionTask.OwnerTaskChain = this;
            if (currentIndex == 0)
            {
                taskList.Add(actionTask);
            }
            else
            {
                if (currentIndex == taskList.Count - 1)
                {
                    //append to last task
                    taskList.Add(actionTask);
                }
                else
                {
                    taskList.Insert(currentIndex + 1, actionTask);
                }

            }
            return actionTask;
        }
        public void Start()
        {
            pleaseStop = false;
            if (taskList.Count > 0)
            {
                currentIndex = 0;

                if (onBeginTask != null)
                {
                    onBeginTask();
                }
                taskList[0].Start();
            }
        }
        public void WhenFinish(Action onFinish)
        {
            this.onFinish = onFinish;
        }
        public void WhenTaskBegin(Action onBeginTask)
        {
            this.onBeginTask = onBeginTask;
        }
        public void Stop()
        {
            //stop the chain
            //but not cancel task execution
            pleaseStop = true;
        }
        public void Next()
        {
            if (pleaseStop)
            {
                //just stop
                //not exec further
            }
            else
            {
                if (currentIndex + 1 < taskList.Count)
                {
                    currentIndex++;
                    if (onBeginTask != null)
                    {
                        onBeginTask();
                    }
                    taskList[currentIndex].Start();
                }
                else
                {
                    //finish
                    if (onFinish != null)
                    {
                        onFinish();
                    }
                }
            }
        }
        public int CurrentTaskIndex
        {
            get
            {
                return currentIndex;
            }
        }
        public int TaskCount
        {
            get
            {
                return taskList.Count;

            }
        }

    }

}