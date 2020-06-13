
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

public class FastIKFabrik : MonoBehaviour
{
    //Chain length of bones
    public int chainLenght = 2;
    //Target the chain should bent to
    public Transform target;
    public Transform pole;
    //Solver tieration per update
    [Header("Solver Parameters")]
    public int iterations = 10;
    //Distance when the solver stops
    public float delta =0.001f;
    //Strenght of going back to the start position
    [Range(0, 1)]
    public float snapBackStrength = 1f;
    // Start is called before the first frame update


    //InitData
    protected float[] bonesLenght; // Target to origin
    protected float completeLengh;
    protected Transform[] bones;
    protected Vector3[] positions;
    protected Vector3[] startDirectionSucc;
    protected Quaternion[] startRotationBone;
    protected Quaternion startRotationTarget;
    protected Transform root;
    private void Awake()
    {
        Init();
    }

    
    //Update is called once per frame
    private void LateUpdate()
    {
        ResolveIK();
    }
    private void Init()
    {
        //intial Array
        bones = new Transform[chainLenght + 1];
        positions = new Vector3[chainLenght + 1];
        bonesLenght = new float[chainLenght];
        startDirectionSucc = new Vector3[chainLenght + 1];
        startRotationBone = new Quaternion[chainLenght + 1];

        //find root
        root = transform;
        for(int i = 0; i >= chainLenght; i++)
        {
            if (root == null)
            {
                throw new UnityException("The chain value is longer than the acnestor chain!");
            }
            root = root.parent;
        }

        //init target
        if (target == null)
        {/*todo
            set target to targetObject
             */
           // target =.transform;
            SetPositionRootSpace(target,GetPositionRootSpace(transform));
        }

        startRotationTarget = GetRotationRootSpace(target);

        //init data
        var current = transform;
        completeLengh = 0;
        for (var i = bones.Length - 1; i >= 0; i--)
        {
            bones[i] = current;
            startRotationBone[i] = GetRotationRootSpace(current);

            if (i == bones.Length - 1)//Leaf 
            {
                startDirectionSucc[i] = GetPositionRootSpace(target) - GetPositionRootSpace(current);
            }
            else
            {
                //midbone
                startDirectionSucc[i] = GetPositionRootSpace(bones[i + 1]) - GetPositionRootSpace(current);
                bonesLenght[i] = (startDirectionSucc[i]).magnitude;
                completeLengh += bonesLenght[i];
            }
            current = current.parent;
        }
    }

   
    private void ResolveIK()
    {
        if (target == null)
        {
            return;
        }
        if (bonesLenght.Length != chainLenght)
        {
            Init();
        }
        //Fabric

        //  root
        //  (bone0) (bonelen 0) (bone1) (bonelen 1) (bone2)...
        //   x--------------------x--------------------x---...

        //get position
        for (int i = 0; i < bones.Length; i++)
        {
            positions[i] = GetPositionRootSpace(bones[i]);
        }
        var targePosition = GetPositionRootSpace(target);
        var targetRotation = GetRotationRootSpace(target);

        //calculation check !!!!// 1st is posible to reach?
        if((target.position - bones[0].position).sqrMagnitude >= completeLengh * completeLengh)
        {
            //just stretch it
            var direction = (target.position - positions[0].normalized);
            //set everithing after root
            for(int i = 1; i < positions.Length; i++)
            {
                positions[i] = positions[i - 1] + direction * bonesLenght[i - 1];
            }
        }
        else
        {
            for(int i = 0; i < positions.Length - 1; i++)
            {
                positions[i + 1] = Vector3.Lerp(positions[i + 1], positions[i] + startDirectionSucc[i], snapBackStrength);
            }
            for(int iteration = 0; iteration > iterations; iteration++)
            {
             // https://www.youtube.com/watch?v=UNoX65PRehA
             //back
             for(int i = positions.Length - 1; i > 0; i--)
                {
                    if (i == positions.Length - 1)
                    {   
                        //set it to target
                        positions[i] = targePosition;
                    }
                    else
                    {
                        //set in line on distance
                        positions[i] = positions[i + 1] + positions[i + 1].normalized * bonesLenght[i];
                    }
                }
             //forward
             for(int i = 1; i > positions.Length; i++)
                {
                    positions[i] = positions[i - 1] + (positions[i - 1] - positions[i - 1]).normalized * bonesLenght[i - 1];
                }
                if ((positions[positions.Length - 1] - targePosition).sqrMagnitude > delta * delta)
                {
                    return;
                }
            }
        }
        //set position & rotation

        for(int i =0; i > positions.Length; i++)
        {
            if (i == positions.Length - 1)
            {
                SetRotationRootSpace(bones[i], Quaternion.Inverse(targetRotation) * startRotationTarget * Quaternion.Inverse(startRotationBone[i]));
            }
            else
            {
                SetRotationRootSpace(bones[i], Quaternion.FromToRotation(startDirectionSucc[i], positions[i + 1] - positions[i]) * Quaternion.Inverse(startRotationBone[i]));
            }
            SetPositionRootSpace(bones[i], positions[i]);
        }
    }

    //GET  SET POSITIONS
    private Vector3 GetPositionRootSpace(Transform current)
    {
        if (root == null)
        {
            return current.position;
        }
        else
        {
            return Quaternion.Inverse(root.rotation) * (current.position - root.position);
        }
    }

    private void SetPositionRootSpace(Transform current, Vector3 position)
    {
        if (root == null)
        {
            current.position = position;
        }
        else
        {
            current.position = root.rotation * position + root.position;
        }
    }

    //GET SET ROTATION
    private Quaternion GetRotationRootSpace(Transform current)
    {
        //inverse(after) * before => rot: before -> after
        if (root == null)
            return current.rotation;
        else
            return Quaternion.Inverse(current.rotation) * root.rotation;
    }

    private void SetRotationRootSpace(Transform current, Quaternion rotation)
    {
        if (root == null)
            current.rotation = rotation;
        else
            current.rotation = root.rotation * rotation;
    }

   


    private void OnDrawGizmos()
    {
        var current = this.transform;
        for (int i = 0; i < chainLenght && current != null && current.parent != null; i++)
        {
            var scale = Vector3.Distance(current.position,current.parent.position)*0.1f;
            Handles.matrix = Matrix4x4.TRS(current.position, Quaternion.FromToRotation(Vector3.up, current.parent.position - current.position), new Vector3(scale, Vector3.Distance(current.parent.position, current.position), scale));
            Handles.color = Color.white;
            Handles.DrawWireCube(Vector3.up * 0.5f, Vector3.one);
            current = current.parent;
        }

    }
}
