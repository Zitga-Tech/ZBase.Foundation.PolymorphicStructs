using System;
using System.Numerics;
using ZBase.Foundation.PolymorphicStructs;

namespace PolymorphicStructTests
{
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine(MyState.GetTypeId<StateC>());
            Console.WriteLine(MyState.GetTypeId<StateA>());
            Console.WriteLine(MyState.GetTypeId<StateB>());
        }
    }

    [Serializable]
    public struct MyStateMachine
    {
        public MyState currentState;

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

    partial struct MyState { }

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
                refData.stateMachine.transitionToStateIndex = (int)MyState.GetTypeId(default(StateB));
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
                refData.stateMachine.transitionToStateIndex = (int)MyState.GetTypeId(default(StateC));
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
                refData.stateMachine.transitionToStateIndex = (int)MyState.GetTypeId(default(StateA));
            }
        }
    }

    [PolymorphicStructInterface]
    public interface IAnimationEvent
    {
        bool IsValid
        {
            [CustomAttributes.Method]
            get;
            set;
        }

        [CustomAttributes.Method]
        void Invoke();
    }

    partial struct AnimationEvent { }

    [PolymorphicStruct]
    public partial struct AnimationAttackEvent : IAnimationEvent
    {
        public bool IsValid
        {
            get => false;
            set { }
        }

        public void Invoke() { }
    }

    [PolymorphicStruct]
    public partial struct AnimationWalkEvent : IAnimationEvent
    {
        public bool IsValid
        {
            get => true;
            set { }
        }

        public void Invoke() { }
    }

    public static class OutTestAPI
    {
        public static int Value = 10;
    }

    [PolymorphicStructInterface]
    public interface IOutTest
    {
        public int Value { get => default; }

        public int Invoke(out int value)
        {
            value = default;
            return default;
        }

        int Invoke();

        public ref int RefOut(ref int a)
        {
            //x = default;
            return ref OutTestAPI.Value;
        }
    }

    partial struct OutTest
    {

    }

    [PolymorphicStruct]
    public partial struct OutTestA : IOutTest
    {
        public int Invoke()
        {
            return default;
        }

        public ref int RefOut(ref int a)
        {
            //x = default;
            return ref OutTestAPI.Value;
        }
    }

    [PolymorphicStructInterface]
    public interface IEmpty
    {
    }

    partial struct Empty { }
}

namespace CustomAttributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MethodAttribute : Attribute { }

}