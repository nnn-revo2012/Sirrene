# Sirrene

ニコニコ動画の~~動画~~・コメントをダウンロードするツールです。

**ニコニコ動画（ニコ動）は2024/05/08に新動画サーバー(DMS)のみの配信となりました。   
DMSで配信する動画はすべてAES128暗号化されており、これを解除する方法の公開やツール作成は日本の著作権法に違反する可能性があります。   
作者(nnn-revo2012)は日本在住なのでSirreneを対応することができません。   
※アメリカ、EU、中国、韓国を含むほとんどの国でもDRM暗号化の解除は違法なのでAES暗号化動画の解除も違法になる可能性があります。   
今後の動画についてはご自分で情報を探すなりして対応してください。**   
# 特徴

- GUI(Windows Forms)使用。  
- さきゅばす1.xxの動画・コメント仕様と互換性があります  

# 開発環境

- Windows 10  
- Microsoft Visual Studio 2019以降  
- .NET 4.8  

# パッケージ

以下のパッケージをインストールしてください。  

- Json.net 13.0.3  
https://www.nuget.org/packages/Newtonsoft.Json/  

- BouncyCastle 1.8.9  
https://www.nuget.org/packages/bouncycastle/  
※BouncyCastle.NetCore は署名されていないので使わない  

- SQLite 1.0.118  
https://www.nuget.org/packages/System.Data.SQLite/  


# 実行方法

実行ファイル・ライブラリーを同じフォルダーに入れて実行してください。  
また、外部プログラムも同じフォルダーに入れてください。  

# ライセンス
- Json.NET  
https://www.newtonsoft.com/json  
Copyright (c) 2007 James Newton-King  
Released under the MIT License  
- BouncyCastle  
http://www.bouncycastle.org/csharp/  
Copyright (c) 2000-2020 Legion of the Bouncy Castle Inc.  
Released under the MIT License  

- SQLite  
https://www.sqlite.org/index.html  
Released into the Public Domain  
