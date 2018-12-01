namespace Ryujinx.HLE.Utilities
{
    internal enum LinuxError
    {
        Success        = 0,
        Perm           = 1       /* Operation not permitted */,
        NoEnt          = 2       /* No such file or directory */,
        Srch           = 3       /* No such process */,
        Intr           = 4       /* Interrupted system call */,
        Io             = 5       /* I/O error */,
        NxIo           = 6       /* No such device or address */,
        TooBig         = 7       /* Argument list too long */,
        NoExec         = 8       /* Exec format error */,
        BadF           = 9       /* Bad file number */,
        Child          = 10      /* No child processes */,
        Again          = 11      /* Try again */,
        NoMem          = 12      /* Out of memory */,
        Acces          = 13      /* Permission denied */,
        Fault          = 14      /* Bad address */,
        NotBlk         = 15      /* Block device required */,
        Busy           = 16      /* Device or resource busy */,
        Exist          = 17      /* File exists */,
        XDev           = 18      /* Cross-device link */,
        NoDev          = 19      /* No such device */,
        NotDir         = 20      /* Not a directory */,
        IsDir          = 21      /* Is a directory */,
        InVal          = 22      /* Invalid argument */,
        NFile          = 23      /* File table overflow */,
        MFile          = 24      /* Too many open files */,
        NoTty          = 25      /* Not a typewriter */,
        TxtBsy         = 26      /* Text file busy */,
        FBig           = 27      /* File too large */,
        NoSpc          = 28      /* No space left on device */,
        SPipe          = 29      /* Illegal seek */,
        RoFs           = 30      /* Read-only file system */,
        MLink          = 31      /* Too many links */,
        Pipe           = 32      /* Broken pipe */,
        Dom            = 33      /* Math argument out of domain of func */,
        Range          = 34      /* Math result not representable */,
        DeadLk         = 35      /* Resource deadlock would occur */,
        NameTooLong    = 36      /* File name too long */,
        NoLck          = 37      /* No record locks available */,

        /*
         * This error code is special: arch syscall entry code will return
         * -ENOSYS if users try to call a syscall that doesn't exist.  To keep
         * failures of syscalls that really do exist distinguishable from
         * failures due to attempts to use a nonexistent syscall, syscall
         * implementations should refrain from returning -ENOSYS.
         */
        NoSys          = 38      /* Invalid system call number */,
        NotEmpty       = 39      /* Directory not empty */,
        Loop           = 40      /* Too many symbolic links encountered */,
        WouldBlock     = Again  /* Operation would block */,
        NoMsg          = 42      /* No message of desired type */,
        IdRm           = 43      /* Identifier removed */,
        ChRng          = 44      /* Channel number out of range */,
        L2NSync        = 45      /* Level 2 not synchronized */,
        L3Hlt          = 46      /* Level 3 halted */,
        L3Rst          = 47      /* Level 3 reset */,
        LnRng          = 48      /* Link number out of range */,
        UnAtch         = 49      /* Protocol driver not attached */,
        NoCsi          = 50      /* No CSI structure available */,
        L2Hlt          = 51      /* Level 2 halted */,
        BadE           = 52      /* Invalid exchange */,
        BadR           = 53      /* Invalid request descriptor */,
        XFull          = 54      /* Exchange full */,
        NoAno          = 55      /* No anode */,
        BadRqC         = 56      /* Invalid request code */,
        BadSlt         = 57      /* Invalid slot */,
        DeadLock       = DeadLk,
        BFont          = 59      /* Bad font file format */,
        NoStr          = 60      /* Device not a stream */,
        NoData         = 61      /* No data available */,
        Time           = 62      /* Timer expired */,
        NoSr           = 63      /* Out of streams resources */,
        NoNet          = 64      /* Machine is not on the network */,
        NoPkg          = 65      /* Package not installed */,
        Remote         = 66      /* Object is remote */,
        NoLink         = 67      /* Link has been severed */,
        Adv            = 68      /* Advertise error */,
        Stmnt          = 69      /* Srmount error */,
        Comm           = 70      /* Communication error on send */,
        Proto          = 71      /* Protocol error */,
        Multihop       = 72      /* Multihop attempted */,
        DotDot         = 73      /* RFS specific error */,
        BadMsg         = 74      /* Not a data message */,
        Overflow       = 75      /* Value too large for defined data type */,
        NotUniq        = 76      /* Name not unique on network */,
        BadFd          = 77      /* File descriptor in bad state */,
        RemChg         = 78      /* Remote address changed */,
        LibAcc         = 79      /* Can not access a needed shared library */,
        LibBad         = 80      /* Accessing a corrupted shared library */,
        LibScn         = 81      /* .lib section in a.out corrupted */,
        LibMax         = 82      /* Attempting to link in too many shared libraries */,
        LibExec        = 83      /* Cannot exec a shared library directly */,
        IlSeq          = 84      /* Illegal byte sequence */,
        Restart        = 85      /* Interrupted system call should be restarted */,
        StrPipe        = 86      /* Streams pipe error */,
        Users          = 87      /* Too many users */,
        NotSock        = 88      /* Socket operation on non-socket */,
        DestAddrReq    = 89      /* Destination address required */,
        MsgSize        = 90      /* Message too long */,
        ProtoType      = 91      /* Protocol wrong type for socket */,
        NoProtoOpt     = 92      /* Protocol not available */,
        ProtoNoSupport = 93      /* Protocol not supported */,
        SocktNoSupport = 94      /* Socket type not supported */,
        OpNotSupp      = 95      /* Operation not supported on transport endpoint */,
        PfNoSupport    = 96      /* Protocol family not supported */,
        AfNoSupport    = 97      /* Address family not supported by protocol */,
        AddrInUse      = 98      /* Address already in use */,
        AddrNotAvail   = 99      /* Cannot assign requested address */,
        NetDown        = 100     /* Network is down */,
        NetUnReach     = 101     /* Network is unreachable */,
        NetReset       = 102     /* Network dropped connection because of reset */,
        ConnAborted    = 103     /* Software caused connection abort */,
        ConnReset      = 104     /* Connection reset by peer */,
        NoBufs         = 105     /* No buffer space available */,
        IsConn         = 106     /* Transport endpoint is already connected */,
        NotConn        = 107     /* Transport endpoint is not connected */,
        Shutdown       = 108     /* Cannot send after transport endpoint shutdown */,
        TooManyRefs    = 109     /* Too many references: cannot splice */,
        TimedOut       = 110     /* Connection timed out */,
        ConnRefused    = 111     /* Connection refused */,
        HostDown       = 112     /* Host is down */,
        HostUnReach    = 113     /* No route to host */,
        Already        = 114     /* Operation already in progress */,
        InProgress     = 115     /* Operation now in progress */,
        Stale          = 116     /* Stale file handle */,
        UClean         = 117     /* Structure needs cleaning */,
        NotNam         = 118     /* Not a XENIX named type file */,
        NAvail         = 119     /* No XENIX semaphores available */,
        IsNam          = 120     /* Is a named type file */,
        RemoteIo       = 121     /* Remote I/O error */,
        DQuot          = 122     /* Quota exceeded */,
        NoMedium       = 123     /* No medium found */,
        MediumType     = 124     /* Wrong medium type */,
        Canceled       = 125     /* Operation Canceled */,
        NoKey          = 126     /* Required key not available */,
        KeyExpired     = 127     /* Key has expired */,
        KeyRevoked     = 128     /* Key has been revoked */,
        KeyRejected    = 129     /* Key was rejected by service */,

        /* for robust mutexes */
        OwnerDead      = 130     /* Owner died */,
        NotRecoverable = 131     /* State not recoverable */,

        RfKill         = 132     /* Operation not possible due to RF-kill */,

        HwPoison       = 133     /* Memory page has hardware error */
    }
}