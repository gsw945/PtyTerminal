## PtyTerminal

[English](./README_zh.md) | **��������**

.NET(C#) ��ƽ̨�� [α�ն�](https://baike.baidu.com/item/%E4%BC%AA%E7%BB%88%E7%AB%AF/6247439) ��, ����ʹ��ʾ��

## ��Ŀ
- **Pty.Net** .NET(C#) ��ƽ̨�� [α�ն�](https://baike.baidu.com/item/%E4%BC%AA%E7%BB%88%E7%AB%AF/6247439) ��
    - Windows ƽ̨�ϼ��� `ConPTY` �� `winpty`
    - �� Unix ƽ̨��ͨ��ƽ̨�������()�ӿ� (`forkpty`��`ioctl`��`kill` ��)ʵ��
        - �ӿ��� Linux ���� `libc.so.6` �� `libutil.so.1` �ṩ
        - �ӿ��� MacOs ���� `libSystem.dylib` �ṩ
- **PtyWeb**
    - **CliDemo** Pty.Net �ڿ���̨��ʹ��ʾ��
    - **WebDemo** Pty.Net �� Web �е�ʹ��ʾ��, ͨ�� [EmbedIO](https://github.com/unosquare/embedio) �� [Xterm.js](https://github.com/xtermjs/xterm.js/) ʵ��
        - TODO: ʹ�� [ASP.NET Core �е� WebSocket ֧��](https://learn.microsoft.com/zh-cn/aspnet/core/fundamentals/websockets) �滻 EmbedIO

## �ο�
- [Windows �����У����� Windows α�ն� (ConPTY)](https://devblogs.microsoft.com/commandline/windows-command-line-introducing-the-windows-pseudo-console-conpty/)
- Github: [rprichard/winpty](https://github.com/rprichard/winpty)
- Github: [microsoft/terminal](https://github.com/microsoft/terminal)
    - [src/winconpty](https://github.com/microsoft/terminal/tree/main/src/winconpty)
    - [samples/ConPTY](https://github.com/microsoft/terminal/tree/main/samples/ConPTY)
- [ƽ̨���÷��� (Platform Invocation Services)](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke), ��� `P/Invoke`
