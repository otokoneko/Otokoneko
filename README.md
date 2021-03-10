## 预览

![](screenshot/1.png)

![](screenshot/2.png)

![](screenshot/3.png)

## 快速开始

1. 分别[下载](https://github.com/otokoneko/Otokoneko/releases)服务端、客户端以及插件压缩包并解压，将 `.dll` 插件放入服务端中的 `plugin` 目录下

2. 启动服务端后会在当前目录下生成 `config.yml` 配置文件，可以通过配置文件进行服务端的配置

![](screenshot/4.png)


3. 选择所需地址的配置字符串在客户端中进行配置，并使用 Root 用户邀请码进行注册（该邀请码仅在数据库初始化时出现，可以通过删除 `data` 目录重置数据库）并登录

## 媒体库扫描

### 推荐的目录结构

程序对媒体库进行扫描时会将媒体库目录下的子目录作为漫画目录，将包含了图片且不包含其它章节目录的目录作为章节目录。如需经常对漫画中的目录结构进行更新，推荐如下的目录结构

```
库
└─漫画
    │  cover.jpg
    │
    ├─卷1
    │  ├─章节1
    │  ├─章节2
    │  └─章节3
    └─卷2
        ├─章节1
        ├─章节2
        └─章节3
```

### 忽略目录

如需忽略特定目录，可以在该目录下创建名为 `.ignore` 的文件

### 支持的文件格式

支持常见的图片格式，如`jpg`、`png`等

支持常见的压缩包格式，如`zip`、`rar`等

## 搜索语句

在漫画搜索中，使用一对 `$` 标识标签，一个搜索示例如下：

```
关键词1 $包含标签1$ -$排除标签2$ ($可选标签3$ ~ $可选标签4$ ~ $可选标签5$)
```

使用类似`-`的语法表示该次搜索需要排除所选的标签

使用类似`( ~ )`的语法表示该次搜索需要包含可选标签中的任意一个