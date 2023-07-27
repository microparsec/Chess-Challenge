using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    private readonly MonteCarlo _mcts;

    public MyBot()
    {
        _mcts = new MonteCarlo();
    }

    public Move Think(Board board, Timer timer)
    {
        _mcts.Initialize(board);
        _mcts.MCTS(timer, 500);
        return _mcts.GetBestMove();
    }
}

public class MonteCarlo
{
    private GameTreeNode Root { get; set; }

    private Board Board { get; set; }

    private readonly static int[] _values = { 0, 100, 300, 300, 500, 900, 500000 };

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

        try
        {
            Root = Root.Children[previousMoves[previousMoves.Length - 2]].Children[previousMoves[previousMoves.Length - 1]];
        }
        catch
        {
            Root = new GameTreeNode() {
                Move = previousMoves[previousMoves.Length - 1],
                Children = new Dictionary<Move, GameTreeNode>()
            };
        }
        
        Root.Parent = null;

        // The Board will need to be rewinded one step back, so that MCTS will traverse the move stack properly from the Root move
        Board.UndoMove(previousMoves[previousMoves.Length - 1]);
    }

    public void MCTS(Timer timer, int limit)
    {
        int sims = Root.Sims;
        while(timer.MillisecondsElapsedThisTurn < limit)
        // /while(true)
        {
            sims += 1;
            // Select the best node
            GameTreeNode node = Selection(sims);
            

            // Move the Board state to the node by following its parents
            Stack<Move> moves = new Stack<Move>();
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

            if(node.Sims > 0)
            {
                Board.MakeMove(node.Move);
                node = Expansion(node, Board);
            }
            Simulation(node, Board);
            Backpropagate(node);

            // Reset the board
            parent = node.Parent;
            while(parent != null)
            {
                Board.UndoMove(parent.Move);
                parent = parent.Parent;
            }   
        }

        foreach(GameTreeNode child in Root.Children.Values.OrderByDescending(node => node.Sims))
        {
            Console.WriteLine($"Move {child.Move}: Sims {child.Sims}, Wins: {child.Wins}, Value: {child.Value}");
        }

    }

    protected GameTreeNode Selection(int totalSims)
    {
        GameTreeNode node = Root;

        while(node.Children.Count > 0)
        {
            node = node.Children.MaxBy(child => CalculateUcb(child.Value, totalSims)).Value;
        }

        return node;
    }

    protected static double CalculateUcb(GameTreeNode node, int totalSims)
    {
        var value = node.Wins / Math.Max(1.0, node.Sims) + 20 * Sigmoid(0.00005 * node.Value * Math.Sqrt( Math.Log(totalSims) / Math.Max(0.00000000001, node.Sims)));
        return value;
    }

    protected GameTreeNode Expansion(GameTreeNode selection, Board board)
    {
        foreach(Move move in board.GetLegalMoves())
        {
            GameTreeNode child = new GameTreeNode()
            {
                Parent = selection,
                Move = move,
                Children = new Dictionary<Move, GameTreeNode>(),
            };
            child.Value = Evaluate(child, board);

            selection.Children.Add(move, child);
        }

        if(selection.Children.Count > 0)
            return selection.Children.First().Value;
        else
        {
            Board.UndoMove(selection.Move);
            return selection;
        }
    }

    protected GameTreeNode Simulation(GameTreeNode node, Board board)
    {
        Board.MakeMove(node.Move);

        int result = 1;
        Stack<Move> moves = new Stack<Move>();
        while(true)
        {
            if(board.IsInCheckmate())
                break;
            if(board.IsDraw())
            {
                result = 0;
                break;
            }

            Move move = board.GetLegalMoves().OrderBy(_ => Guid.NewGuid()).First();
            moves.Push(move);

            board.MakeMove(move);
            result *= -1;
        }

        foreach(Move move in moves)
        {
            board.UndoMove(move);
        }
        node.SimResult = result;


        Board.UndoMove(node.Move);

        return node;
    }

    protected void Backpropagate(GameTreeNode? node)
    {
        int result = node.SimResult;
        int opponent = 0;
        while(node != null)
        {
            node.Sims += 1;
            if(opponent == 1 && result == -1)
                node.Wins += 1;
            if(opponent == 0 && result == 1)
                node.Wins += 1;
            node = node.Parent;

            if(opponent == 0) opponent = 1;
            else opponent = 0;
        }

    }

    protected static double Sigmoid(double value)
    {
        return 1.0 / (1 + Math.Pow(Math.E, -value));
    }

    protected int Evaluate(GameTreeNode node, Board board)
    {
        int valueFlipper = Board.IsWhiteToMove ? 1 : -1;
        board.MakeMove(node.Move);

        if(board.IsInCheckmate())
        {
            board.UndoMove(node.Move);
            return int.MaxValue;
        }
        if(board.IsDraw())
        {
            board.UndoMove(node.Move);
            return 0;
        }

        int value = 0;

        Stack<Move> rewindCaptures = new Stack<Move>();
        Move[] movesWithCaptures = board.GetLegalMoves(true);
        while(movesWithCaptures.Length > 0)
        {
            Move leastValueableCapture = movesWithCaptures.OrderBy(move => move.MovePieceType - move.CapturePieceType).First();
            board.MakeMove(leastValueableCapture);
            rewindCaptures.Push(leastValueableCapture);
            movesWithCaptures = board.GetLegalMoves(true);
        }

        foreach(PieceList pieces in board.GetAllPieceLists())
            value += pieces.Count * (pieces.IsWhitePieceList ? 1 : -1) * _values[(int)pieces.TypeOfPieceInList] * valueFlipper;

        value += board.GetLegalMoves().Length;


        foreach(Move rewindCapture in rewindCaptures)
        {
            board.UndoMove(rewindCapture);
        }

        board.UndoMove(node.Move);
        return value;
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

    public int SimResult { get; set; }

    public int Value { get; set; }

    public Dictionary<Move, GameTreeNode> Children { get; set; }
}