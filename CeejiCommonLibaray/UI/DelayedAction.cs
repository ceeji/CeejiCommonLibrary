/*
 * Copyright(c) Ceeji Cheng, 2014 - 2015
 * All rights reserved
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ceeji.UI {
    /// <summary>
    /// a class which represents one delayed action
    /// </summary>
    /// <remarks>
    /// <para>When it is fired, the action will delay for some time and then be executed. </para>
    /// <para>If user fire the action for more than one time in this period, this action will only be excuted once</para>
    /// <para>The action and the WaitingTimeSpan parameters can be changed when StartWaiting being called with the same name twice</para>
    /// </remarks>
    public class DelayedAction {
        private DelayedAction(string name, Action<object> action, TimeSpan waitPeriod, object state = null) {
            Name = name;
            Action = action;
            WaitingTime = waitPeriod;
            State = state;
            HasState = true;
        }

        private DelayedAction(string name, Action action, TimeSpan waitPeriod, object state = null) {
            Name = name;
            Action = action;
            WaitingTime = waitPeriod;
            State = state;
            HasState = false;
        }

        static DelayedAction() {
            list = new List<DelayedAction>();
        }

        /// <summary>
        /// Starts waiting for an action to excute. If action with same name be started more then one time in the period, the action will be excuted only once. This method is thread-safe
        /// </summary>
        /// <param name="name">The name for this action. Same action can only have one name</param>
        /// <param name="action">A delegate to be excuted</param>
        /// <param name="waitPeriod">A TimeSpan which represents the period to wait before action being excuted</param>
        /// <param name="state">A state to be sent to the delegate</param>
        public static DelayedAction StartWaiting(string name, Action<object> action, TimeSpan waitPeriod, object state = null) {
            lock (list) {
                var actionWrapper = getAction(name);
                if (actionWrapper == null) {
                    actionWrapper = new DelayedAction(name, action, waitPeriod, state);
                    list.Add(actionWrapper);
                }

                // add Timer
                if (actionWrapper.InternalTimer != null) {
                    actionWrapper.InternalTimer.Close();
                }
                actionWrapper.InternalTimer = new System.Timers.Timer(waitPeriod.TotalMilliseconds);
                actionWrapper.InternalTimer.Elapsed += actionWrapper.InternalTimer_Elapsed;
                actionWrapper.InternalTimer.AutoReset = false;
                actionWrapper.InternalTimer.Start();

                return actionWrapper;
            }
        }

        private static DelayedAction getAction(string name) {
            return list.FirstOrDefault(x => x.Name == name);
        }

        /// <summary>
        /// Starts waiting for an action to excute. If action with same name be started more then one time in the period, the action will be excuted only once. This method is thread-safe
        /// </summary>
        /// <param name="name">The name for this action. Same action can only have one name</param>
        /// <param name="action">A delegate to be excuted</param>
        /// <param name="waitPeriod">A TimeSpan which represents the period to wait before action being excuted</param>
        /// <param name="state">A state to be sent to the delegate</param>
        public static DelayedAction StartWaiting(string name, Action action, TimeSpan waitPeriod, object state = null) {
            lock (list) {
                var actionWrapper = getAction(name);
                if (actionWrapper == null) {
                    actionWrapper = new DelayedAction(name, action, waitPeriod, state);
                    list.Add(actionWrapper);
                }
                else {
                    actionWrapper.Action = action;
                    actionWrapper.State = state;
                }

                // add Timer
                if (actionWrapper.InternalTimer != null) {
                    actionWrapper.InternalTimer.Close();
                }
                actionWrapper.InternalTimer = new System.Timers.Timer(waitPeriod.TotalMilliseconds);
                actionWrapper.InternalTimer.Elapsed += actionWrapper.InternalTimer_Elapsed;
                actionWrapper.InternalTimer.AutoReset = false;
                actionWrapper.InternalTimer.Start();

                return actionWrapper;
            }
        }

        private void InternalTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            lock (list) {
                if (HasState) {
                    Action.DynamicInvoke(State);
                }
                else {
                    Action.DynamicInvoke();
                }
                InternalTimer.Close();

                // remove itself from list

                list.Remove(this);
            }
        }

        /// <summary>
        /// Gets the name of this action
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Gets the delegate to be excuted
        /// </summary>
        public Delegate Action { get; private set; }
        /// <summary>
        /// Gets the TimeSpan before excuting action
        /// </summary>
        public TimeSpan WaitingTime { get; private set; }
        /// <summary>
        /// Gets the State
        /// </summary>
        public object State { get; private set; }

        private System.Timers.Timer InternalTimer { get; set; }
        private bool HasState { get; set; }

        private static List<DelayedAction> list;
    }
}
