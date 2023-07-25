using System;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    private readonly Random _random;

    int prev = 0;

    public MyBot()
    {
        _random = new Random();
    }

    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();

        ValueMove[] valueMoves = moves.OrderBy(a => Guid.NewGuid()).Select(a => new ValueMove() {value = 0, move = a}).ToArray();

        for(int i = 0; i < valueMoves.Length && timer.MillisecondsElapsedThisTurn < 2000; i++ )
        {
            for(int  j = 0; j < 500; j++)
            {
                valueMoves[i].value += DoMonteCarlo(board);
            }
        }
        
        if(board.IsWhiteToMove)
            valueMoves = valueMoves.OrderByDescending(a => a.value).ToArray();
        else
            valueMoves = valueMoves.OrderBy(a => a.value).ToArray();

        foreach(ValueMove move in valueMoves)
        {
            Console.WriteLine($"Move {move.move.ToString()}: {move.value}");
        }
        Console.WriteLine("---------------------");

        return valueMoves[0].move;
    }

    int DoMonteCarlo(Board board)
    {
        if(board.IsDraw())
            return 0;
        if(board.IsInCheckmate())
            return board.IsWhiteToMove ? -1 : 1;

        Move[] moves = board.GetLegalMoves();
        
        var valueMoves = moves.Select(a => 
            {
                board.MakeMove(a);
                int value = GetBoardValue(board);
                board.UndoMove(a);
                return new ValueMove() { move = a, value = value };
            });

        if(board.IsWhiteToMove)
            valueMoves.OrderByDescending(a => a.value);
        else 
            valueMoves.OrderBy(a => a.value);

        Move newMove = moves[_random.Next(0, Math.Min(6, moves.Length))];
        board.MakeMove(newMove);
        int returnValue = DoMonteCarlo(board);
        board.UndoMove(newMove);

        return returnValue;
    }

    int GetBoardValue(Board board)
    {
        int value = 0;
        foreach(PieceList pieceList in board.GetAllPieceLists())
        {
            foreach(Piece piece in pieceList)
            {
                value += getPieceValue(piece);
            }
        }

        return value;
    }

    int getPieceValue(Piece piece)
    {
        int value = 0;
        switch(piece.PieceType)
        {
            case PieceType.Pawn:
                value = 1;
                break;
            case PieceType.Rook:
                value = 5;
                break;
            case PieceType.Bishop:
                value = 3;
                break;
            case PieceType.Knight:
                value = 2;
                break;
            case PieceType.Queen:
                value = 9;
                break;
            case PieceType.King:
                value = 5000;
                break;
            default:
                value = 0;
                break;
        }            

        return (piece.IsWhite ? 1 : -1) * value;
    }
}

struct ValueMove
{
    public Move move;
    public int value;
}
