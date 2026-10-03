using System;
using System.Collections.Generic;
using System.Linq;

namespace LearningFoundry.Core
{
    public static class LabMissions
    {
        public static readonly string[] Ids = {"C01","C02","C03","C04","C05","D01","D02","D03","D04","D05","D06","E01","E02","E03","E04","E05","E06","F01","F02","F03","F04","F05","F06","F07","F08","F09","G01","G02","G03","G04","S01","S02","S03","S04","S05","S06","S07","S08"};
        public const int Partition=1, Validation=2, Gradient=4, Shape=8, WrongPair=16, Compared=32, Packaged=64, Fitted=128, Drawn=256, PersonalSplit=512, Statistics=1024, Frozen=2048, WindowMoved=4096;
        static LayerSpec L(TensorOp op,int width=2,int k=3,int s=1,int p=0) { return new LayerSpec{op=op,width=width,kernel=k,stride=s,padding=p,title=Name(op)}; }
        static LayerSpec Affine(int input,int output,float[] weights,float[] bias=null) {var l=L(TensorOp.Dense,output);l.Init(input);l.weights=weights;l.bias=bias??new float[output];return l;}
        static List<LayerSpec> SmallClassifier(int classes=10,int width=32) { return new List<LayerSpec>{L(TensorOp.Flatten),L(TensorOp.Dense,width),L(TensorOp.Relu),L(TensorOp.Dense,classes)}; }
        public static LabWorkspace Initial(string id,Profile p)
        {
            var w=new LabWorkspace{levelId=id};
            string previous=id=="C01"?null:id=="E06"?"E05":id=="G01"||id=="G02"?"E05":id=="G03"?"G02":id=="G04"?"G03":id=="S05"||id=="S06"?"E05":id=="S08"?"G04":null;
            var old=p.labs==null?null:p.labs.Find(a=>a.levelId==previous);
            if(old!=null && old.layers.Count>0)w.layers=ModelIO.Copy(old.layers);
            if(id=="C01")w.layers=new List<LayerSpec>{Affine(2,2,new[]{1f,-1f,-1f,1f})};
            if(id=="E01"||id=="E06"||id=="G01"||(previous!=null&&w.layers.Count==0))w.layers=SmallClassifier();
            if(id=="S01"){w.layers.Add(L(TensorOp.Multiply));w.layers.Add(L(TensorOp.Multiply));w.layers.Add(L(TensorOp.Multiply));foreach(var l in w.layers)l.weights=new[]{2f,2f,2f};w.layers[1].weights=new[]{1f,1f,1f};w.layers[2].weights=new[]{1f,1f,1f};}
            if(id=="G01"||id=="G02"||id=="G03"||id=="G04")w.trainCount=10000;
            if(id=="G03"||id=="G04") { if(old!=null){w.steps=old.steps;w.cursor=old.cursor;w.records=ModelIO.Copy(old.records);w.trainCurve=new List<float>(old.trainCurve);w.validationCurve=new List<float>(old.validationCurve);} }
            if(id=="S06"&&old!=null){var m=LabData.Measure(old,"validation",512);w.records.Add(LabData.Record(old,m,"原机 / 固定验证"));w.layers.Clear();}
            if(id=="S05")w.trainCount=96;
            return w;
        }
        public static Tensor Input(string id,int variant)
        {
            float[] signed={-2f,0,3f}; if(id=="C02"||id=="C04"||id=="S01")return new Tensor(new[]{3},variant%2==0?signed:new[]{4f,-3f,0});
            if(id=="C03")return new Tensor(new[]{2,2},variant%2==0?new[]{1f,2f,-1f,3f}:new[]{-2f,.5f,3f,-1f});
            if(id.StartsWith("D")) { if(id=="D01"||id=="D03")return new Tensor(new[]{3},variant%2==0?new[]{-2f,3f,1f}:new[]{4f,-1f,2f});return new Tensor(new[]{2,3},variant%2==0?new[]{1f,2f,3f,-2f,0,4f}:new[]{-1f,3f,0,2f,-3f,5f}); }
            if(id=="E02"||id=="E03"||id=="E04") { var a=variant%4==0?new[]{-2f,0,1f,2f,-1f,4f,3f,0,-2f,1f}:variant%4==1?Enumerable.Repeat(1000f,10).ToArray():variant%4==2?new[]{0f,0,0,0,0,-8f,0,0,0,0}:new[]{0f,0,0,0,0,8f,0,0,0,0};return new Tensor(new[]{1,10},a); }
            if(id=="S04")return new Tensor(new[]{4,3},variant==0?new[]{1f,100,7,2,200,7,3,300,7,4,400,7}:new[]{5f,500,7,-1,-100,7,2.5f,250,7,0,0,7});
            if(id=="S02")return new Tensor(new[]{3},new[]{-2f,3f,.1f});
            bool pool=id=="F06"||id=="F07"||id=="S07";int size=pool?4:8,channels=id=="F04"?2:1;var data=new float[size*size*channels];
            for(int c=0;c<channels;c++)for(int y=0;y<size;y++)for(int x=0;x<size;x++)data[(c*size+y)*size+x]=pool?((y*size+x+variant)%7-5):(((x+variant+c*2)>3?1f:0f)+(y==3?.5f:0));
            if(id=="F07"&&variant==2){data[0]=data[1]=data[4]=data[5]=9;}
            return new Tensor(new[]{1,channels,size,size},data);
        }
        public static List<LayerSpec> Reference(string id)
        {
            var list=new List<LayerSpec>();
            switch(id)
            {
                case "C02":list.Add(L(TensorOp.Gate));break;
                case "C03":list.Add(Affine(2,2,new[]{1f,0,0,1}));list.Add(Affine(2,1,new[]{2f,-1},new[]{.5f}));break;
                case "C04":var gate=L(TensorOp.GateBackward,3);list.Add(gate);break;
                case "D01":list.Add(L(TensorOp.Pack));list.Add(L(TensorOp.Unpack));break;
                case "D02":list.Add(new LayerSpec{op=TensorOp.Reshape,reshape=new[]{6}});list.Add(new LayerSpec{op=TensorOp.Reshape,reshape=new[]{2,3}});break;
                case "D03":var multiply=L(TensorOp.Multiply);multiply.weights=new[]{2f,-1,.5f};list.Add(multiply);list.Add(L(TensorOp.Sum));break;
                case "D04":list.Add(L(TensorOp.Transpose));var mat=L(TensorOp.MatMul,2);mat.Init(2);mat.weights=new[]{1f,0,0,2f};list.Add(mat);break;
                case "D05":list.Add(Affine(3,2,new[]{1f,0,0,0,1f,0},new[]{.5f,-.5f}));break;
                case "D06":list.Add(L(TensorOp.MeanBatch));break;
                case "E02":list.Add(L(TensorOp.Argmax));break;
                case "E03":list.Add(L(TensorOp.Softmax));break;
                case "E04":list.Add(L(TensorOp.CrossEntropy));break;
                case "F01":case "F02":case "F03":case "F04":case "F05":
                    var conv=L(id=="F01"?TensorOp.Window:id=="F02"?TensorOp.Scan:TensorOp.Conv2D,id=="F04"?2:1,3,id=="F05"?2:1,id=="F05"?1:0);conv.Init(id=="F04"?2:1,true);
                    if(id=="F01"||id=="F02") {conv.learnable=false;conv.weights=new[]{-1f,0,1,-1f,0,1,-1f,0,1};}list.Add(conv);break;
                case "F06":case "F07":list.Add(L(TensorOp.MaxPool,1,2,2));break;
                case "F08":list.Add(L(TensorOp.Flatten));break;
                case "F09":var vision=L(TensorOp.Vision);vision.children.Add(L(TensorOp.Conv2D,2));vision.children.Add(L(TensorOp.Relu));vision.children.Add(L(TensorOp.MaxPool,1,2,2));list.Add(vision);break;
                case "S01":var scale=L(TensorOp.Multiply);scale.weights=new[]{2f,2,2};list.Add(scale);break;
                case "S04":var norm=L(TensorOp.Normalize);TensorEngine.FitNormalize(norm,Input(id,0));list.Add(norm);break;
                case "S07":list.Add(L(TensorOp.AvgPool,1,2,2));break;
            }
            return list;
        }
        public static TensorOp[] Palette(string id)
        {
            switch(id)
            {
                case "C01":case "C03":return new[]{TensorOp.Dense};
                case "C02":return new[]{TensorOp.Gate,TensorOp.Multiply};
                case "C04":return new[]{TensorOp.GateBackward,TensorOp.Gate};
                case "C05":case "S03":return new[]{TensorOp.Dense,TensorOp.Relu};
                case "D01":return new[]{TensorOp.Pack,TensorOp.Unpack};
                case "D02":return new[]{TensorOp.Reshape};
                case "D03":return new[]{TensorOp.Multiply,TensorOp.Sum};
                case "D04":return new[]{TensorOp.Transpose,TensorOp.MatMul};
                case "D05":return new[]{TensorOp.Dense,TensorOp.MatMul};
                case "D06":return new[]{TensorOp.MeanBatch,TensorOp.Sum};
                case "E02":return new[]{TensorOp.Argmax,TensorOp.Softmax};
                case "E03":return new[]{TensorOp.Softmax,TensorOp.Argmax};
                case "E04":return new[]{TensorOp.CrossEntropy,TensorOp.Softmax};
                case "F01":return new[]{TensorOp.Window};case "F02":return new[]{TensorOp.Scan};
                case "F03":case "F04":case "F05":return new[]{TensorOp.Conv2D};
                case "F06":case "F07":return new[]{TensorOp.MaxPool};case "F08":return new[]{TensorOp.Flatten,TensorOp.Reshape};
                case "F09":return new[]{TensorOp.Conv2D,TensorOp.Relu,TensorOp.MaxPool};
                case "S01":return new[]{TensorOp.Multiply,TensorOp.Sum};
                case "S04":return new[]{TensorOp.Normalize};case "S07":return new[]{TensorOp.AvgPool,TensorOp.MaxPool};
                default:return id.StartsWith("G")||id=="S08"?new[]{TensorOp.Conv2D,TensorOp.MaxPool,TensorOp.AvgPool,TensorOp.Flatten,TensorOp.Dense,TensorOp.Relu,TensorOp.Vision}:new[]{TensorOp.Flatten,TensorOp.Dense,TensorOp.Relu};
            }
        }
        public static string Name(TensorOp op)
        { switch(op){case TensorOp.Pack:return "向量打包";case TensorOp.Unpack:return "元素拆包";case TensorOp.Reshape:return "形状转接器";case TensorOp.Multiply:return "逐项乘法";case TensorOp.Sum:return "求和器";case TensorOp.Transpose:return "转置器";case TensorOp.MatMul:return "矩阵乘法";case TensorOp.Dense:return "全连接层";case TensorOp.Gate:return "正值闸门";case TensorOp.GateBackward:return "梯度闸门";case TensorOp.Relu:return "ReLU";case TensorOp.Softmax:return "概率刻度";case TensorOp.Argmax:return "最大值判别";case TensorOp.CrossEntropy:return "分类误差计";case TensorOp.Window:return "窗口点积";case TensorOp.Scan:return "滑动扫描";case TensorOp.Conv2D:return "卷积层";case TensorOp.MaxPool:return "最大池化";case TensorOp.AvgPool:return "平均池化";case TensorOp.Flatten:return "展平器";case TensorOp.Normalize:return "标准化";case TensorOp.MeanBatch:return "平均归约";case TensorOp.Vision:return "视觉积木";default:return op.ToString();} }
        public static string Lesson(TensorOp op)
        {switch(op){case TensorOp.Dense:return "每个输出有独立的一行权重和一个偏置：y=W·x+b。宽度是输出数量。反向会把同一偏置在所有样本中的贡献相加。";case TensorOp.Conv2D:return "一个卷积核在各个窗口共用同一组权重。窗口重叠时，回到同一像素的梯度须相加。输入顺序为 N,C,H,W。";case TensorOp.MaxPool:return "每个窗口只保留最大值。回流给获胜位置；并列取行优先的第一个。负数也参与比较。";case TensorOp.AvgPool:return "窗口内求平均。梯度均分给每个位置；重叠窗口的贡献仍需相加。";case TensorOp.Flatten:return "保留第一个批量轴，把其余轴按通道、行、列顺序展开。元素数和反向对应关系不变。";case TensorOp.Softmax:return "先减去最大分数，再取指数并归一化。概率均非负、和为1；所有分数加同一个常量不改变概率。";case TensorOp.CrossEntropy:return "输入原始分数。内部以 log-sum-exp 稳定计算 −log(真类别概率)，回流为 (概率−one-hot)/批量数。";case TensorOp.Relu:case TensorOp.Gate:return "输入 >0 时通行，≤0 输出0。ReLU 的回流只在正数处通行；零处采用梯度0。";case TensorOp.GateBackward:return "输入代表 ReLU 前的数值，上游 g 由‘宽度 / g’旋钮设置。正数输出g，负数与零输出0。";case TensorOp.Normalize:return "只用训练数据拟合每列均值与总体标准差，验证数据沿用同一刻度。零标准差改用1。";case TensorOp.Argmax:return "比较同一行的原始分数，输出最高分的下标；并列取第一个。此硬判别只用于读数，不能传递训练梯度。";case TensorOp.Window:case TensorOp.Scan:return "3×3核与当前位置的九个像素逐项乘再相加。此处核固定为右边减左边；移动读数游标查看每个窗口。";case TensorOp.Vision:return "封装一段 Conv→ReLU→Pool 线路。每次安装都会复制内部参数，可展开检查形状和回流。";case TensorOp.Reshape:return "只换外形，不换元素和行优先顺序。输入与目标元素总数必须一致，回流按同一顺序映射。";case TensorOp.Pack:case TensorOp.Unpack:return "把有顺序的数组成一个向量，再拆成逐个读数。第一项到最后一项的位置必须保持。";case TensorOp.Multiply:return "同长度两组数逐项相乘。此模块的第二组数在权重栏中设置；回流乘以对应系数。";case TensorOp.Sum:return "所有元素相加。上游梯度会原样回到每一项。";case TensorOp.MeanBatch:return "所有样本与元素的平均值：和除以数量。回流也除以数量，这让批量大小不改变平均梯度的尺度。";case TensorOp.Transpose:return "矩阵行列交换，元素 [r,c] 到 [c,r]，回流使用逆向同样映射。";case TensorOp.MatMul:return "输入 [N,F] 乘每个输出的 F 项权重，输出 [N,O]。权重栏按‘输出行’存放；反向用相应转置计算。";default:return "安装模块、送入样本并查看每一项的实际测量。";} }
        public static string Objective(string id)
        {
            switch(id){case "C01":return "这是诊断关：线性模型无需解出 XOR。观察全部4点，调权重记录两条不同边界；每条都应有错点。";case "C02":return "安装正值闸门：[-2,0,3] → [0,0,3]，另一组输入也要成立。";case "C03":return "串联两个 Dense：先宽度2，权重 [1,0,0,1]；再宽度1，权重 [2,-1]，偏置 [0.5]。观察中间坐标，最终 y=2x−z+0.5。";case "C04":return "安装梯度闸门，设置 g=3。正数回3，负数与零回0；验收还会检查 g=−2。";case "C05":return "自行安装 Dense→ReLU→Dense，末层宽度2。推荐隐藏宽度8、学习率0.12。实际训练后 XOR 四点全部正确。";case "D01":return "安装打包→拆包，保持三个元素顺序。切换两组样本，选中元素查看对应。";case "D02":return "安装两个形状转接器：[2,3]→[6]→[2,3]。另外尝试 [5]，记录元素数不符的报错，再修复。";case "D03":return "用逐项乘法的权重 [2,-1,0.5] 接求和器，构成点积。切换输入，并检查非单位回流。";case "D04":return "安装转置→矩阵乘法。转置 [2,3]→[3,2]；乘法宽度2，权重 [1,0,0,2]。检查回流。";case "D05":return "安装 Dense 宽度2，权重 [1,0,0,0,1,0]，偏置 [0.5,-0.5]。送样与回流，再保存/复制，确认副本参数独立。";case "D06":return "安装平均归约。检查 [2,3] 的6项均值；对非单位梯度观察每项 g/6，与逐项累加一致。";case "E01":return "数据柜有训练集、验证集、密封测试集。打开训练与验证抽屉，各测量一次；测试集必须保持封存。";case "E02":return "安装最大值判别，观察10个类别分数。验收包含负分数与并列，后者取第一个。";case "E03":return "安装概率刻度。送入不同分数，包括1000分的并列；比较整体加常数后的输出。";case "E04":return "原始分数直接接分类误差计，真标签5。观察自信正确/犹豫/自信错误三组误差与概率减标签的回流。";case "E05":return "从28×28灰度图开始，安装展平→Dense→ReLU→Dense(10)。训练集2000件，固定验证2000件。真实验证达到85%，保存检查点。";case "E06":return "在固定验证集上生成混淆矩阵。选中一个有错例的格子查看图像；改变配置并保存两次实测记录比较。";case "F01":return "安装窗口点积，3×3右减左固定核。点击特征图不同格子，观察对应九个像素；切换两张图。";case "F02":return "安装滑动扫描，固定3×3右减左核。逐格移动窗口，观察完整6×6响应图。";case "F03":return "安装 Conv2D：1通道、3×3核、步幅1、填充0。检查核与输入的数值梯度，尤其重叠窗口的相加。";case "F04":return "输入2通道。安装输出2通道的 Conv2D，3×3核。切换输出通道观察不同响应，再检查各自权重与偏置回流。";case "F05":return "8×8输入，3×3卷积、输出1通道。设置步幅2、填充1；先填写预测输出形状 [1,1,4,4] 再测量。";case "F06":return "安装2×2最大池化，步幅2。全部负数的窗口也取正确最大值，输出2×2。";case "F07":return "安装2×2最大池化，步幅2。检查上游g=3的回流，第三张图包含并列最大值，只流回首个位置。";case "F08":return "安装展平器。[1,1,8,8]→[1,64]；验收还含批量2与通道2，批量轴不能混进特征。";case "F09":return "安装 Conv(2通道,3×3)→ReLU→MaxPool(2×2,步幅2)。封装为视觉积木，保存并复制；展开核对前向与回流。";case "G01":return "将训练抽屉扩为10000件，验证仍为原来的2000件。核对图像0～1、标签0～9；独立测试保持封存。";case "G02":return "自由组合一个可训练的10分类器，参数≤50000。至少保存两种不同结构；可用Dense，也可加入视觉积木。";case "G03":return "用同一数据、种子、学习率与步数比较两种结构。训练和验证曲线同时记录，固定验证达到90%，保存模型。训练可暂停续跑。";case "G04":return "冻结当前参数，再首次打开官方独立测试集。测试达到88%；在手写板上画一个数字，查看28×28输入与推理结果，再导出模型。";case "S01":return "这条设备有3个逐项乘法，其中2个是多余的。把它缩成1个、系数[2,2,2]，新输入结果保持相同。";case "S02":return "动量是带记忆的更新：v=0.8v+g，w=w−0.1v。实验台可切换SGD/动量。观察并保存两条真实轨迹，再填写第2步动量参数值。";case "S03":return "安装2类 Dense→ReLU→Dense，隐藏宽度可调。训练固定种子双月，独立噪声验证准确率达到95%，查看弯曲边界。";case "S04":return "安装标准化，点击‘拟合训练刻度’。仅用训练四行拟合均值/总体标准差；第三列恒定7，以标准差1保护。验证沿用同一刻度。";case "S05":return "用96件训练数据观察过拟合，固定2000件验证。记录至少三个不同步数的检查点，再按验证误差选择较早检查点进行比较。";case "S06":return "用 Dense 层搭一个小于原机参数量的分类器，相同2000件训练与固定验证，达到80%。保存与原机实测比较。";case "S07":return "安装2×2平均池化，步幅2，检查非单位回流均分。再与最大池化对极端像素的响应比较。";case "S08":return "保存至少4件个人训练笔迹、2件独立验证笔迹，分别标注标签。冻结原机记录个人验证，微调≥20步后再次记录比较；测试笔迹不进入更新。";default:return "按技术手册完成实验。";}
        }
        static bool Has(LabWorkspace w,TensorOp op,int minimum=1) {return w.layers.Count(l=>l.op==op)>=minimum;}
        public static int ParameterCount(LabWorkspace w) { var b=LabData.IsClassifier(w.levelId)?LabData.Batch(w,"validation",0,1).input:Input(w.levelId,0);TensorEngine.Run(w.layers,b,5);return w.layers.Sum(l=>l.Parameters); }
        public static CheckResult Check(LabWorkspace w)
        {
            try{
                if(w.layers.Count==0&&w.levelId!="S02")return CheckResult.Fail("还没有安装模块。打开左侧备件箱，接成一条从输入到读数的线路。");
                var result=new CheckResult{passed=true};string id=w.levelId;
                Func<int,bool> did=flag=>(w.actions&flag)==flag;
                if(id=="C01") {var m=LabData.Measure(w,"validation",4);return Verdict(w.observed.Distinct().Count()>=4&&w.signatures.Distinct().Count()>=2&&m.accuracy<1,"观察4点并记录两条不同且有错点的线性边界。");}
                if(id=="E01"||id=="G01")return Verdict(did(Partition|Validation)&&w.partition!=2&&(id!="G01"||w.trainCount==10000),"训练和验证抽屉都需测量，独立测试保持封存。G01训练数量10000。");
                if(id=="S02") {double v=0,pos=-2;for(int i=0;i<2;i++){v=.8*v+pos-2;pos-=.1*v;}return Verdict(w.records.Count>=2&&Math.Abs(w.prediction/1000.0-pos)<.002,"比较SGD与动量两条轨迹，填写第2步动量w（答案保留3位）。");}
                if(LabData.IsTrainable(id)||id=="E06"){
                    int count=id=="C05"?4:id=="S03"?400:id=="S08"?w.personal.Count(d=>d.validation):2000;if(count<1)return CheckResult.Fail("还没有独立个人验证样本。");
                    var m=LabData.Measure(w,id=="S08"?"personal-validation":"validation",count);float target=id=="C05"?.999f:id=="E05"?.85f:id=="S03"?.95f:id=="S06"?.8f:id=="G03"?.90f:0;
                    result.rows.Add("实际验证："+m.count+" 件 / 正确率 "+m.accuracy.ToString("P1")+" / CE "+m.loss.ToString("0.000"));
                    if(id=="C05")result.passed=Has(w,TensorOp.Dense,2)&&Has(w,TensorOp.Relu)&&w.steps>=20&&m.accuracy>=target;
                    if(id=="E05")result.passed=Has(w,TensorOp.Flatten)&&Has(w,TensorOp.Dense,2)&&Has(w,TensorOp.Relu)&&w.steps>=20&&m.accuracy>=target&&w.records.Count>0;
                    if(id=="E06")result.passed=did(WrongPair|Compared)&&w.records.Count>=2;
                    if(id=="G02")result.passed=w.records.Select(r=>r.architecture).Distinct().Count()>=2&&ParameterCount(w)<=50000&&ParameterCount(w)>0;
                    if(id=="G03") {var records=w.records.GroupBy(r=>r.architecture).Select(g=>g.Last()).ToArray();result.passed=m.accuracy>=target&&w.steps>=100&&records.Length>=2&&records.Any(a=>records.Any(b=>a.architecture!=b.architecture&&a.fromFreshInitialization&&b.fromFreshInitialization&&a.seed==b.seed&&a.dataset==b.dataset&&a.steps==b.steps&&Math.Abs(a.rate-b.rate)<1e-6));}
                    if(id=="G04") {if(!w.tested||!did(Frozen|Drawn)||w.frozenHash!=ModelIO.Hash(w.layers))return CheckResult.Fail("先冻结并验收独立测试，再画一个数字并导出冻结模型。修改权重后必须重新冻结。");var test=LabData.Measure(w,"test",2000);result.rows.Add("密封测试：2000件 / "+test.accuracy.ToString("P1"));result.passed=test.accuracy>=.88f&&w.signatures.Contains("export:"+w.frozenHash);}
                    if(id=="S05")result.passed=w.records.Select(r=>r.steps).Distinct().Count()>=3&&did(Compared)&&w.trainCount==96;
                    if(id=="S06")result.passed=w.records.Count>0&&ParameterCount(w)<w.records[0].layers.Sum(l=>l.Parameters)&&!w.layers.Any(l=>l.op==TensorOp.Conv2D||l.op==TensorOp.Vision)&&m.accuracy>=target&&w.steps>=20&&did(Compared);
                    if(id=="S03")result.passed=Has(w,TensorOp.Dense,2)&&Has(w,TensorOp.Relu)&&w.steps>=20&&m.accuracy>=target;
                    if(id=="S08")result.passed=w.personal.Count(d=>!d.validation)>=4&&w.personal.Count(d=>d.validation)>=2&&w.steps>=20&&did(PersonalSplit|Compared)&&w.records.Count>=2;
                    result.message=result.passed?"真实数据验收通过，已保留模型与实验记录。":"尚未符合本关规格。"+(target>0?"目标验证正确率 "+target.ToString("P0")+"。":"")+Objective(id);return result;
                }
                var required=Reference(id);if(required.Count==0)return CheckResult.Fail("本关实验尚未配置。");
                bool structure=required.GroupBy(l=>l.op).All(g=>Has(w,g.Key,g.Count()));if(!structure)return CheckResult.Fail("需要使用本关引入的模块完成装配。"+Objective(id));
                if(id=="S01"&&w.layers.Count!=1)return CheckResult.Fail("结果相同还不够：缩为一个有效模块。");
                if(id=="F09"&&(!did(Packaged)||w.layers.Count!=1||w.layers[0].children.Count!=3))return CheckResult.Fail("先搭好 Conv→ReLU→Pool，再封装为视觉积木。");
                if(id=="F05"&&(!did(Shape)||w.shapePrediction.Replace(" ","").Replace("[","").Replace("]","")!="1,1,4,4"))return CheckResult.Fail("先填写预测形状 1,1,4,4，再测量。");
                for(int variant=0;variant<3;variant++){
                    var x=Input(id,variant);if(id=="F08"&&variant==2)x=new Tensor(new[]{2,2,2,3},Enumerable.Range(0,24).Select(i=>(float)i).ToArray());
                    var reference=TensorEngine.Run(required,x.Copy(),5);var actual=TensorEngine.Run(w.layers,x.Copy(),5);
                    bool values=actual.output.shape.SequenceEqual(reference.output.shape)&&actual.output.data.Length==reference.output.data.Length&&actual.output.data.Zip(reference.output.data,(a,b)=>Math.Abs(a-b)<.0001).All(v=>v);
                    if(id=="F03"||id=="F04"||id=="F05"||id=="F09")values=actual.output.shape.SequenceEqual(reference.output.shape);
                    if(id=="C04") {var gate=w.layers.First(l=>l.op==TensorOp.GateBackward);int old=gate.width;gate.width=-2;var negative=TensorEngine.Run(w.layers,x.Copy());gate.width=old;values&=negative.output.data.Select((a,i)=>Math.Abs(a-(x.data[i]>0?-2:0))<1e-6).All(v=>v);}
                    result.passed&=values;result.rows.Add("样本 "+variant+"："+actual.output.Shape+" / "+(values?"前向 ✓":"数值或形状 ×"));
                    if(!values)continue;
                    if(new[]{"D02","D03","D04","D05","D06","F03","F04","F05","F07","F08","F09","S07"}.Contains(id)){
                        actual.Backward(Enumerable.Repeat(3f,actual.output.data.Length).ToArray());reference.Backward(Enumerable.Repeat(3f,reference.output.data.Length).ToArray());
                        if(id!="F03"&&id!="F04"&&id!="F05"&&id!="F09")result.passed&=actual.input.grad.Zip(reference.input.grad,(a,b)=>Math.Abs(a-b)<.0001).All(v=>v);
                        if(id=="F03"||id=="F04"||id=="F09"){var smooth=x.Copy();if(id=="F09")for(int j=0;j<smooth.data.Length;j++)smooth.data[j]+=(j%17+1)*.0031f;result.passed&=GradientCheck(w.layers,smooth,5).passed;}
                    }
                }
                if(new[]{"D03","D04","D05","D06","F03","F04","F07","F09","S07"}.Contains(id))result.passed&=did(Gradient);
                if(id=="D02")result.passed&=w.signatures.Contains("invalid-shape");
                if(id=="D05"||id=="F09")result.passed&=did(Compared);
                if(id=="F01"||id=="F02")result.passed&=did(WindowMoved)&&w.observed.Distinct().Count()>=2;
                if(id=="S04")result.passed&=did(Fitted|Validation);
                if(id=="S07")result.passed&=did(Compared);
                if(new[]{"C02","C03","C04","D01","E02","E03","E04","F06","F08"}.Contains(id))result.passed&=w.observed.Distinct().Count()>=2;
                result.message=result.passed?"三组独立输入与回流检查通过。":"数值、形状或实验记录未达标。"+Objective(id);return result;
            }catch(Exception e){return CheckResult.Fail("检查停止："+e.Message);}
        }
        static CheckResult Verdict(bool okay,string message){return new CheckResult{passed=okay,message=okay?"实验记录通过验收。":message};}
        static string Branch(TensorRun r){return string.Join("/",r.steps.Select(s=>s.routing!=null?string.Join(",",s.routing):s.layer.op==TensorOp.Relu||s.layer.op==TensorOp.Gate?string.Join("",s.input.data.Select(v=>v>0?"1":"0")):""));}
        public static CheckResult GradientCheck(List<LayerSpec> layers,Tensor input,int label=5)
        {
            var r=TensorEngine.Run(layers,input,label);r.Backward(Enumerable.Repeat(3f,r.output.data.Length).ToArray());var result=new CheckResult{passed=true};float eps=.0005f;int checkedCount=0,skipped=0;string branch=Branch(r);
            Func<TensorRun> evaluate=()=>TensorEngine.Run(layers,input.Copy(),label);
            Func<TensorRun,double> value=run=>run.output.data.Sum(v=>(double)v)*3;
            for(int i=0;i<Math.Min(input.data.Length,24);i++){float old=input.data[i];input.data[i]=old+eps;var plus=evaluate();input.data[i]=old-eps;var minus=evaluate();input.data[i]=old;if(Branch(plus)!=branch||Branch(minus)!=branch){skipped++;continue;}checkedCount++;double measured=(value(plus)-value(minus))/(2*eps),actual=r.input.grad[i];bool good=Math.Abs(measured-actual)<.04*(1+Math.Abs(actual));result.passed&=good;if(i<4||!good)result.rows.Add("输入["+i+"] 回流 "+actual.ToString("0.###")+" / 数值 "+measured.ToString("0.###")+(good?" ✓":" ×"));}
            foreach(var s in r.steps.Where(t=>t.dw!=null))for(int i=0;i<Math.Min(s.dw.Length,12);i++){float old=s.layer.weights[i];s.layer.weights[i]=old+eps;var plus=evaluate();s.layer.weights[i]=old-eps;var minus=evaluate();s.layer.weights[i]=old;if(Branch(plus)!=branch||Branch(minus)!=branch){skipped++;continue;}checkedCount++;double measured=(value(plus)-value(minus))/(2*eps),actual=s.dw[i];bool good=Math.Abs(measured-actual)<.05*(1+Math.Abs(actual));result.passed&=good;if(i==0||!good)result.rows.Add(Name(s.layer.op)+" 核/权重["+i+"] "+actual.ToString("0.###")+" / "+measured.ToString("0.###")+(good?" ✓":" ×"));}
            result.passed&=checkedCount>0;result.rows.Add(checkedCount+"项平滑位置 / "+skipped+"项跨过闸门或最大值折点，单独按约定检查");
            result.message=result.passed?"平滑位置的非单位回流与数值变化率一致。":"回流与数值变化率有差异。";return result;
        }
    }
}
