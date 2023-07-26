using System;
using System.Collections.Generic;
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

    IDictionary<ulong, int> zobrist;

    int evals = 0;

    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();

        ValueMove[] valueMoves = moves.OrderBy(a => Guid.NewGuid()).Select(a => new ValueMove() {value = 0, move = a}).ToArray();

        zobrist = new Dictionary<ulong, int>();
        evals=0;

        for(int i = 0; i < valueMoves.Length && timer.MillisecondsElapsedThisTurn < 20000; i++ )
        {
            for(int  j = 0; j < 100; j++)
            {
                int result = DoMonteCarlo(board);
                valueMoves[i].value += result;
                if(result == 1)
                    valueMoves[i].result.whiteWon++;
                
                if(result == -1)
                    valueMoves[i].result.blackWon++;

                if(result == 0)
                    valueMoves[i].result.Draw++;
            }
        }
        
        if(board.IsWhiteToMove)
            valueMoves = valueMoves.OrderByDescending(a => a.value).ToArray();
        else
            valueMoves = valueMoves.OrderBy(a => a.value).ToArray();

        foreach(ValueMove move in valueMoves)
        {
            Console.WriteLine($"Move {move.move.ToString()}: {move.value} ({move.result.whiteWon}|-{move.result.blackWon}|={move.result.Draw})");
        }
        Console.WriteLine($"---------------------evals: {evals}");

        return valueMoves[0].move;
    }

    int DoMonteCarlo(Board board)
    {
        if(board.IsDraw())
            return 0;
        if(board.IsInCheckmate())
            return board.IsWhiteToMove ? -1 : 1;

        Move[] moves = board.GetLegalMoves();
        
        var valueMoves = moves.OrderBy(a => Guid.NewGuid()).Select(a => 
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

        Move newMove = valueMoves.ToArray()[_random.Next(0, Math.Min(4, moves.Length))].move;
        board.MakeMove(newMove);
        int returnValue = DoMonteCarlo(board);
        board.UndoMove(newMove);

        return returnValue;
    }

    int GetBoardValue(Board board, int depth = 0, int alpha = int.MinValue, int beta = int.MaxValue)
    {
        if(depth == 3)
            return GetBoardPieceValue(board);
        
        if(board.IsInCheckmate())
            return board.IsWhiteToMove ? 50000 : -50000;
        
        if(board.IsDraw())
            return 0;
        
        int value = 0;
        if(board.IsWhiteToMove)
        {
            value = int.MinValue;
            foreach(Move move in board.GetLegalMoves())
            {
                board.MakeMove(move);
                value = Math.Max(value, GetBoardValue(board, depth + 1, alpha, beta));
                board.UndoMove(move);
                if(value > beta)
                    break;
                alpha = Math.Max(value, alpha);
            }
        } 
        else
        {
            value = int.MaxValue;
            foreach(Move move in board.GetLegalMoves())
            {
                board.MakeMove(move);
                value = Math.Min(value, GetBoardValue(board, depth + 1));
                board.UndoMove(move);

                if(value < alpha)
                    break;
                beta = Math.Min(value, beta);
            }
        }

        return value;
    }

    int GetBoardPieceValue(Board board)
    {
        evals++;
        ulong zobristKey = board.ZobristKey;
        if(zobrist.ContainsKey(zobristKey))
            return zobrist[zobristKey];

        int value = 0;
        value += board.GetPieceList(PieceType.Pawn, true).Count * 1;
        value += board.GetPieceList(PieceType.Knight, true).Count * 3;
        value += board.GetPieceList(PieceType.Bishop, true).Count * 3;
        value += board.GetPieceList(PieceType.Rook, true).Count * 5;
        value += board.GetPieceList(PieceType.Queen, true).Count * 9;
        value += board.GetPieceList(PieceType.King, true).Count * 100000;

        value -= board.GetPieceList(PieceType.Pawn, false).Count * 1;
        value -= board.GetPieceList(PieceType.Knight, false).Count * 3;
        value -= board.GetPieceList(PieceType.Bishop, false).Count * 3;
        value -= board.GetPieceList(PieceType.Rook, false).Count * 5;
        value -= board.GetPieceList(PieceType.Queen, false).Count * 9;
        value -= board.GetPieceList(PieceType.King, false).Count * 100000;

        zobrist.Add(zobristKey, value);

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

    public Simresults result;
}

struct Simresults
{
    public int whiteWon;
    public int blackWon;

    public int Draw;
}