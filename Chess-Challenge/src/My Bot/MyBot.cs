using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    private readonly IMonteCarlo _mcts;

    public MyBot()
    {
        _mcts = new BasicMonteCarlo();
    }

    public Move Think(Board board, Timer timer)
    {
        _mcts.Initialize(board);
        _mcts.MCTS(timer, 2000);
        return _mcts.GetBestMove();
    }
}

public interface IMonteCarlo
{
    void Initialize(Board board);

    void MCTS(Timer timer, int limit);

    Move GetBestMove();
}

public class BasicMonteCarlo : IMonteCarlo
{
    private GameTreeNode Root { get; set; }

    private Board Board { get; set; }

    public BasicMonteCarlo()
    {

    }

    public void Initialize(Board board)
    {
        Board = board;

        if(Root == null)
        {
            Root = new GameTreeNode() {
                Move = new Move(),
                Children = new Dictionary<Move, GameTreeNode>()
            };
            return;
        }

        Move[] previousMoves = board.GameMoveHistory;

        Root = Root.Children[previousMoves[previousMoves.Length - 2]].Children[previousMoves[previousMoves.Length - 1]];
        Root.Parent = null;

        // The Board will need to be rewinded one step back, so that MCTS will traverse the move stack properly from the Root move
        Board.UndoMove(previousMoves[previousMoves.Length - 1]);
    }

    public void MCTS(Timer timer, int limit)
    {
        while(timer.MillisecondsElapsedThisTurn < limit)
        //while(true)
        {
            // Select the best node
            GameTreeNode node = Selection();
            

            // Move the Board state to the node by following its parents
            Stack<Move> moves = new Stack<Move>();
            moves.Push(node.Move);
            GameTreeNode? parent = node.Parent;
            while(parent != null)
            {
                moves.Push(parent.Move);
                parent = parent.Parent;
            }
            foreach(Move move in moves)
            {
                Board.MakeMove(move);
            }

            // Expansion
            Expansion(node, Board);

            // Reset the board
            Board.UndoMove(node.Move);
            parent = node.Parent;
            while(parent != null)
            {
                Board.UndoMove(parent.Move);
                parent = parent.Parent;
            }           
        }

    }

    protected GameTreeNode Selection()
    {
        GameTreeNode node = Root;

        while(node.Children.Count > 0 && !node.Children.Any(node => node.Value.Children == null))
        {
            node = SelectBestChild(node);
        }

        if(node.Children.Count > 0)
            node = SelectBestChild(node);

        return node;
    }

    protected GameTreeNode SelectBestChild(GameTreeNode node)
    {
        return node.Children.First().Value;
    }

    protected GameTreeNode Expansion(GameTreeNode selection, Board board)
    {
        selection.Children = new Dictionary<Move, GameTreeNode>();
        foreach(Move move in board.GetLegalMoves())
        {
            selection.Children.Add(move, new GameTreeNode()
            {
                Parent = selection,
                Move = move,
                Children = new Dictionary<Move, GameTreeNode>()
            });
        }

        return selection;
    }

    protected GameTreeNode Simulation(GameTreeNode expansion)
    {
        return expansion;
    }

    public Move GetBestMove()
    {
        return Root.Children.OrderByDescending(node => node.Value.Sims).First().Value.Move;
    }
}

public class GameTreeNode
{
    public Move Move { get; set; }

    public GameTreeNode? Parent { get; set; }

    public int Wins { get; set; } = 0;
    public int Sims { get; set; } = 0;

    public Dictionary<Move, GameTreeNode> Children { get; set; }
}