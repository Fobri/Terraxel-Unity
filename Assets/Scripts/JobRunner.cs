using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;

public abstract class JobRunner
{
    static List<JobRunner> activeJobRunners = new List<JobRunner>();
    public static void Update(){
        for(int i = 0; i < activeJobRunners.Count; i++){
            activeJobRunners[i].CheckIsReadyAndComplete();
        }
    }

    JobHandle jobHandle = default;
    bool hasCompleted = true;
    internal void ScheduleParallelJob<T>(T job, int length) where T : struct, IJobParallelFor{
        if(IsReady){
            activeJobRunners.Add(this);
        }
        jobHandle = JobHandle.CombineDependencies(jobHandle, IJobParallelForExtensions.Schedule(job, length, 32, default));
        hasCompleted = false;
    }
    internal void ScheduleJob<T>(T job, int length) where T : struct, IJobFor{
        if(IsReady){
            activeJobRunners.Add(this);
        }
        jobHandle = JobHandle.CombineDependencies(jobHandle, IJobForExtensions.Schedule(job, length, default));
        hasCompleted = false;
    }
    internal void CheckIsReadyAndComplete(){
        if(jobHandle.Equals(default)) return;
        if(jobHandle.IsCompleted && !hasCompleted){
            jobHandle.Complete();
            jobHandle = default;
            hasCompleted = true;
            activeJobRunners.Remove(this);
            OnJobsReady();
        }
    }
    internal bool IsReady{
        get{
            if(jobHandle.Equals(default)) return true;
            return jobHandle.IsCompleted && hasCompleted;
        }
    }
    internal abstract void OnJobsReady();
    public void CompleteJobs(){
        jobHandle.Complete();
    }
}
