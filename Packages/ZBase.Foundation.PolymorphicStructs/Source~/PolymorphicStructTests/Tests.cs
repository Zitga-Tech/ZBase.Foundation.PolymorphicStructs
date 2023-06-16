using ZBase.Foundation.PolymorphicStructs;

namespace PolymorphicStructTests
{
    public class Program
    {
        public static void Main()
        {

        }
    }

    [PolymorphicStructInterface]
    public interface IState
    {
        void Enter();

        void Exit();

        void Update(float deltaTime);
    }

    [PolymorphicStruct]
    public partial struct IdleState : IState
    {
        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public void Update(float deltaTime)
        {
        }
    }

    [PolymorphicStruct]
    public partial struct WalkState : IState
    {
        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public void Update(float deltaTime)
        {
        }
    }


}