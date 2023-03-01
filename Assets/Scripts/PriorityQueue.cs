using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class PriorityQueue<T> where T : class
{
    private Queue<T>[] queues;
    public PriorityQueue(int queueAmount){
        queues = new Queue<T>[queueAmount];
        for(int i = 0; i < queueAmount; i++){
            queues[i] = new Queue<T>();
        }
    }
    public Queue<T> this[int index]{
        get{
            return queues[index];
        }
        set{
            queues[index] = value;
        }
    }
    public void Enqueue(T t, int queue){
        queues[queue].Enqueue(t);
    }
    public bool TryDequeue(int queue, out T t){
        if(queues[queue].Count > 0){
            t = queues[queue].Dequeue();
            return true;
        }
        t = null;
        return false;
    }
    public T Dequeue(){
        for(int i = queues.Length - 1; i >= 0; i--){
            if(queues[i].Count > 0) return queues[i].Dequeue();
        }
        return null;
    }
    public int PeekQueue(){
        for(int i = queues.Length - 1; i >= 0; i--){
            if(queues[i].Count > 0) return i;
        }
        return -1;
    }
    public int Count{
        get{
            int c = 0;
            for(int i = 0; i < queues.Length; i++){
                c += queues[i].Count;
            }
            return c;
        }
    }
}
