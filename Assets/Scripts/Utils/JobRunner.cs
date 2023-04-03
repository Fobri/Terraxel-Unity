using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;

public abstract class JobRunner
{
    static List<JobRunner> activeJobRunners = new List<JobRunner>();
    public static void CompleteAll(){
        foreach(var job in activeJobRunners){
            job.CompleteJobs();
        }
    }
    public static void Update(){
        for(int i = 0; i < activeJobRunners.Count; i++){
            activeJobRunners[i].CheckIsReadyAndComplete();
        }
    }

    JobHandle jobHandle = default;
    bool hasCompleted = true;
    internal void ScheduleParallelForJob<T>(T job, int length, bool dependency = false) where T : struct, IJobParallelFor{
        if(IsReady){
            activeJobRunners.Add(this);
        }
        if(!dependency)
            jobHandle = JobHandle.CombineDependencies(jobHandle, IJobParallelForExtensions.Schedule(job, length, 32, default));
        else
            jobHandle = IJobParallelForExtensions.Schedule(job, length, 32, jobHandle);
        hasCompleted = false;
    }
    internal void ScheduleJobFor<T>(T job, int length, bool dependency = false) where T : struct, IJobFor{
        if(IsReady){
            activeJobRunners.Add(this);
        }
        if(!dependency)
            jobHandle = JobHandle.CombineDependencies(jobHandle, IJobForExtensions.Schedule(job, length, default));
        else
            jobHandle = IJobForExtensions.Schedule(job, length, jobHandle);
        hasCompleted = false;
    }
    internal void ScheduleJob<T>(T job, bool dependency = false) where T : struct, IJob{
        if(IsReady){
            activeJobRunners.Add(this);
        }
        if(!dependency)
            jobHandle = JobHandle.CombineDependencies(jobHandle, IJobExtensions.Schedule(job, default));
        else
            jobHandle = IJobExtensions.Schedule(job, jobHandle);
        hasCompleted = false;
    }
    internal void CheckIsReadyAndComplete(){
        if(jobHandle.Equals(default)) return;
        if(jobHandle.IsCompleted && !hasCompleted){
            jobHandle.Complete();
            jobHandle = default;
            hasCompleted = true;
            activeJobRunners.Remove(this);
            JobsReady();
        }
    }
    internal bool IsReady{
        get{
            if(jobHandle.Equals(default)) return true;
            return jobHandle.IsCompleted && hasCompleted;
        }
    }
    internal abstract void JobsReady();
    public void CompleteJobs(){
        jobHandle.Complete();
    }
}
