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
        _mcts.MCTS(timer, 750);
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

            Simulation(node, Board);

            Backpropagate(node);

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

        while(node.Children.Count > 0 && !node.Children.Any(node => node.Value.Sims == 0))
        {
            node = SelectBestChild(node);
        }

        if(node.Children.Count > 0)
            node = SelectBestChild(node);

        return node;
    }

    protected GameTreeNode SelectBestChild(GameTreeNode node)
    {
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

        GameTreeNode best = null!;
        int bestValue = int.MinValue;
        GameTreeNode[] nodes = node.Children.OrderBy(_ => Guid.NewGuid()).Select(n => n.Value).ToArray();
        int valueFlipper = Board.IsWhiteToMove ? 1 : -1;

        foreach(GameTreeNode child in nodes)
        {
            int value = 0;

            Board.MakeMove(child.Move);

            if(Board.IsInCheckmate())
            {
                best = child;
                Board.UndoMove(child.Move);
                break;
            }
            if(Board.IsDraw())
            {
                value = 0;
                Board.UndoMove(child.Move);
                continue;
            }

            Stack<Move> rewindCaptures = new Stack<Move>();
            Move[] movesWithCaptures = Board.GetLegalMoves(true);
            while(movesWithCaptures.Length > 0)
            {
                Move leastValueableCapture = movesWithCaptures.OrderBy(move => move.MovePieceType - move.CapturePieceType).First();
                Board.MakeMove(leastValueableCapture);
                rewindCaptures.Push(leastValueableCapture);
                movesWithCaptures = Board.GetLegalMoves(true);
            }

            foreach(PieceList pieces in Board.GetAllPieceLists())
                value += pieces.Count * (pieces.IsWhitePieceList ? 1 : -1) * _values[(int)pieces.TypeOfPieceInList] * valueFlipper;

            foreach(Move rewindCapture in rewindCaptures)
            {
                Board.UndoMove(rewindCapture);
            }

            Board.UndoMove(child.Move);

            if(value > bestValue)
            {
                best = child;
                bestValue = value;
            }
        }

        // Reset the board
        Board.UndoMove(node.Move);
        parent = node.Parent;
        while(parent != null)
        {
            Board.UndoMove(parent.Move);
            parent = parent.Parent;
        }   

        return best;
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

    protected GameTreeNode Simulation(GameTreeNode expansion, Board board)
    {
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
        expansion.SimResult = result;

        return expansion;
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

    public Dictionary<Move, GameTreeNode> Children { get; set; }
}