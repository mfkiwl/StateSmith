﻿using StateSmith.compiler.Visitors;
using StateSmith.compiler;
using StateSmith.Common;
using System.Linq;
using System.Collections.Generic;

#nullable enable

namespace StateSmith.Compiling
{
    public class HistoryContinueProcessor : DummyVertexVisitor
    {
        public override void Visit(HistoryContinueVertex hc)
        {
            var statesToTrack = hc.Siblings<NamedVertex>().ToList();

            foreach (var h in hc.historyVertices)
            {
                ShallowHistoryProcessor.TrackStates(h, null, statesToTrack);
            }

            hc.ParentState.RemoveChild(hc);
        }
    }

    public class ShallowHistoryProcessor : DummyVertexVisitor
    {
        Statemachine sm;

        public ShallowHistoryProcessor(Statemachine sm)
        {
            this.sm = sm;
        }


        public override void Visit(ShallowHistoryVertex historyState)
        {
            sm.historyStates.Add(historyState);
            historyState.stateTrackingVarName = $"{historyState.ParentState.Name}_history_state_tracking_var_name___$$$$"; // will be changed later on with expansions
            Behavior defaultTransition = historyState.Behaviors.Single();
            defaultTransition.guardCode = $"{historyState.stateTrackingVarName} == 0";

            var statesToTrack = historyState.Siblings<NamedVertex>().ToList();
            TrackStates(historyState, defaultTransition, statesToTrack);
        }

        public static void TrackStates(ShallowHistoryVertex historyState, Behavior? defaultTransition, List<NamedVertex> statesToTrack)
        {
            foreach (var stateToTrack in statesToTrack)
            {
                var index = historyState.Behaviors.Count;
                bool isDefaultTransition = stateToTrack == defaultTransition?.TransitionTarget;
                if (isDefaultTransition)
                {
                    index = 0;
                }

                {
                    Behavior trackingBehavior = new Behavior(trigger: TriggerHelper.TRIGGER_ENTER, actionCode: $"{historyState.stateTrackingVarName} = {index};");
                    stateToTrack.AddBehavior(trackingBehavior, 0);
                }

                if (!isDefaultTransition)
                {
                    Behavior transitionBehavior = new Behavior(guardCode: $"{historyState.stateTrackingVarName} == {index}", transitionTarget: stateToTrack);
                    historyState.AddBehavior(transitionBehavior);
                }
            }

            historyState.trackedStateCount = historyState.Behaviors.Count;  // todo - remove trackedStateCount
        }

        public override void Visit(HistoryContinueVertex hc)
        {
            FindTrackingHistoryStates(hc, hc);
        }

        private static void FindTrackingHistoryStates(HistoryContinueVertex hc, Vertex? node)
        {
            node = node?.Parent?.Parent;

            while (node != null)
            {
                hc.historyVertices.AddRange(node.Children<ShallowHistoryVertex>());

                foreach (var v in node.Children<HistoryContinueVertex>())
                {
                    FindTrackingHistoryStates(hc, v);    // todolow - could avoid upwards recursion if we did a breadth first traversal first. Then if we find another $HC, we can just copy its history states
                }

                node = node?.Parent?.Parent;
            }
        }
    }
}
