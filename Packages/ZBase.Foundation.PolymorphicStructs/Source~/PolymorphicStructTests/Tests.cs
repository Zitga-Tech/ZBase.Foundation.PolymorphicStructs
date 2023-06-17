using System;
using System.Numerics;
using ZBase.Foundation.PolymorphicStructs;

namespace PolymorphicStructTests
{
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine(MyStateStruct.GetTypeId<StateC>());
            Console.WriteLine(MyStateStruct.GetTypeId<StateA>());
            Console.WriteLine(MyStateStruct.GetTypeId<StateB>());
        }
    }

    [Serializable]
    public struct MyStateMachine
    {
        public MyStateStruct currentState;

        public float speed;
        public Vector3 startTranslation;
        public bool isInitialized;
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
                refData.stateMachine.transitionToStateIndex = (int)MyStateStruct.GetTypeId(default(StateB));
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
            refData.rotation = Quaternion.Identity;
        }

        public void OnStateUpdate(ref StateUpdateData_RW refData, in StateUpdateData_RO inData)
        {
            refData.rotation = Quaternion.CreateFromAxisAngle(rotationSpeed * inData.DeltaTime, 0f) * refData.rotation;

            _durationCounter -= inData.DeltaTime;

            if (_durationCounter <= 0f)
            {
                refData.stateMachine.transitionToStateIndex = (int)MyStateStruct.GetTypeId(default(StateC));
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
                refData.stateMachine.transitionToStateIndex = (int)MyStateStruct.GetTypeId(default(StateA));
            }
        }
    }

    partial struct MyStateStruct
    {
        //static MyStateStruct()
        //{
        //    TypeId<StateA>.Id = 5;
        //}

        //private struct TypeId<T>
        //{
        //    public static int Id;
        //}
    }
}