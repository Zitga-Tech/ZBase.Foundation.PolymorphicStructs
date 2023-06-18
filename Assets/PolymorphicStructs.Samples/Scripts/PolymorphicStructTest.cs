using System;
using UnityEngine;
using ZBase.Foundation.PolymorphicStructs;

namespace PolymorphicStructs.Samples
{
    public class PolymorphicStructTests : MonoBehaviour
    {
        private void Start()
        {
        }
    }

    [Serializable]
    public struct MyStateMachine
    {
        [HideInInspector]
        public MyStateStruct currentState;

        [HideInInspector]
        public float speed;

        [HideInInspector]
        public Vector3 startTranslation;

        [HideInInspector]
        public bool isInitialized;

        [HideInInspector]
        public int transitionToStateIndex;
    }

    public struct StateUpdateData_RW
    {
        public MyStateMachine stateMachine;

        public Vector3 translation;
        public Quaternion rotation;
        public Vector3 scale;
    }

    public readonly struct StateUpdateData_RO
    {
        public readonly float DeltaTime;
    }

    [PolymorphicStructInterface]
    public interface IMyState
    {
        public void OnStateEnter(ref StateUpdateData_RW refData, in StateUpdateData_RO inData);

        public void OnStateExit(ref StateUpdateData_RW refData, in StateUpdateData_RO inData);

        public void OnStateUpdate(ref StateUpdateData_RW refData, in StateUpdateData_RO inData);
    }

    [Serializable]
    [PolymorphicStruct]
    public partial struct StateA : IMyState
    {
        public float duration;
        public Vector3 movementSpeed;

        private float _durationCounter;

        public void OnStateEnter(ref StateUpdateData_RW refData, in StateUpdateData_RO inData)
        {
            _durationCounter = duration;
        }

        public readonly void OnStateExit(ref StateUpdateData_RW refData, in StateUpdateData_RO inData)
        {
            refData.translation = refData.stateMachine.startTranslation;
        }

        public void OnStateUpdate(ref StateUpdateData_RW refData, in StateUpdateData_RO inData)
        {
            refData.translation += movementSpeed * inData.DeltaTime;

            _durationCounter -= inData.DeltaTime;

            if (_durationCounter <= 0f)
            {
                refData.stateMachine.transitionToStateIndex = (int)MyStateStruct.GetTypeId<StateB>();
            }
        }
    }

    [Serializable]
    [PolymorphicStruct]
    public partial struct StateB : IMyState
    {
        public float duration;
        public Vector3 rotationSpeed;

        private float _durationCounter;

        public void OnStateEnter(ref StateUpdateData_RW refData, in StateUpdateData_RO inData)
        {
            _durationCounter = duration;
        }

        public readonly void OnStateExit(ref StateUpdateData_RW refData, in StateUpdateData_RO inData)
        {
            refData.rotation = Quaternion.identity;
        }

        public void OnStateUpdate(ref StateUpdateData_RW refData, in StateUpdateData_RO inData)
        {
            refData.rotation = Quaternion.Euler(rotationSpeed * inData.DeltaTime) * refData.rotation;

            _durationCounter -= inData.DeltaTime;

            if (_durationCounter <= 0f)
            {
                refData.stateMachine.transitionToStateIndex = (int)MyStateStruct.GetTypeId<StateC>();
            }
        }
    }

    [Serializable]
    [PolymorphicStruct]
    public partial struct StateC : IMyState
    {
        public float duration;
        public float scaleSpeed;

        private float _durationCounter;

        public void OnStateEnter(ref StateUpdateData_RW refData, in StateUpdateData_RO inData)
        {
            _durationCounter = duration;
        }

        public readonly void OnStateExit(ref StateUpdateData_RW refData, in StateUpdateData_RO inData)
        {
            refData.scale = new Vector3(1f, 1f, 1f);
        }

        public void OnStateUpdate(ref StateUpdateData_RW refData, in StateUpdateData_RO inData)
        {
            refData.scale += new Vector3(scaleSpeed, scaleSpeed, scaleSpeed) * inData.DeltaTime;

            _durationCounter -= inData.DeltaTime;

            if (_durationCounter <= 0f)
            {
                refData.stateMachine.transitionToStateIndex = (int)MyStateStruct.GetTypeId<StateA>();
            }
        }
    }

    [PolymorphicStructInterface]
    public interface IAnimationEvent
    {
        void Invoke();
    }

    [PolymorphicStruct]
    public partial struct AnimationAttackEvent : IAnimationEvent
    {
        public int a;

        public void Invoke() { }
    }

    [PolymorphicStruct]
    public partial struct AnimationWalkEvent : IAnimationEvent
    {
        public void Invoke() { }
    }
}